﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace KSPAdvancedFlyByWire
{
    class EVAController
    {
        /**
         * Current EVA Bugs:
         * Jetpack toggle leaves Kerbal in wrong animation state
         * Interact on ladder gives NullReference if not near a hatch
         */


        private const float EVARotationStep = 57.29578f; // From KerbalEVA.UpdateHeading()

        private List<FieldInfo> vectorFields;
        private List<FieldInfo> floatFields;
        private List<FieldInfo> eventFields;
        private FieldInfo colliderListField;
        private KFSMEventCondition ladderStopConditionDelegate;
        private KFSMEventCondition swimStopConditionDelegate;
        private KFSMEventCondition eventConditionDisabled = ((KFSMState s) => false);
        private bool m_autoRunning = false;

        private static EVAController instance;

        public static EVAController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new EVAController();
                }
                return instance;
            }
        }

        public EVAController()
        {
            LoadReflectionFields();
        }

        public void UpdateEVAFlightProperties(ControllerConfiguration config, FlightCtrlState state)
        {
            if ( !FlightGlobals.ActiveVessel.isEVA )
                return;

            KerbalEVA eva = GetKerbalEVA();

            if (this.m_autoRunning)
            {
                state.Z = 1f;
            }

            if (!state.isNeutral)
            {
                Vector3 moveDirection = Vector3.zero;
                Vector3 ladderDirection = Vector3.zero;

                //Debug.Log("State: " + eva.fsm.currentStateName + " (< " + eva.fsm.lastEventName + ")");
                //Debug.Log("MovementMode: " + eva.CharacterFrameMode);
                //Debug.Log("Landed? "+ eva.part.GroundContact +", "+ eva.vessel.LandedOrSplashed +", "+ eva.vessel.Landed);

                if (state.Y != 0)
                {
                    moveDirection += (!eva.CharacterFrameMode ? eva.fUp : eva.transform.up) * state.Y;
                    ladderDirection += state.Y > 0 ? eva.transform.up : -eva.transform.up;
                }
                if (state.X != 0)
                {
                    moveDirection += (!eva.CharacterFrameMode ? eva.fRgt : eva.transform.right) * state.X;
                    ladderDirection += (state.X > 0 ? eva.transform.right : -eva.transform.right);
                }
                if (state.Z != 0)
                {
                    moveDirection += (!eva.CharacterFrameMode ? eva.fFwd : eva.transform.forward) *state.Z;
                    ladderDirection += (state.Z > 0 ? eva.transform.up : -eva.transform.up);
                }

                if (eva.vessel.LandedOrSplashed && !eva.JetpackDeployed && !eva.OnALadder && !eva.isRagdoll)
                {
                    float moveSpeed = moveDirection.magnitude;
                    if (Vector3.Angle(moveDirection, eva.transform.forward) > 45)
                    {
                        // Decrease max moveSpeed when not moving in a forwards direction
                        moveSpeed = Mathf.Clamp(moveSpeed, 0, 0.25f);
                    }
                    this.SetMoveSpeed(moveSpeed);
                }
                if (eva.OnALadder && (state.Y != 0 || state.Z != 0))
                {
                    DisableLadderStopCondition(eva);
                }
                if (eva.vessel.Splashed && (state.X != 0 || state.Z != 0))
                {
                    DisableSwimStopCondition(eva);
                }

                //Debug.LogWarning("Runspeed: " + moveDirection.magnitude);
                moveDirection.Normalize();
                //Debug.LogWarning("MoveDirection: "+ moveDirection);
                this.vectorFields[0].SetValue(eva, moveDirection);              //vector3_0 = MoveDirection
                this.vectorFields[2].SetValue(eva, moveDirection);              //vector3_2 = JetpackDirection
                this.vectorFields[6].SetValue(eva, ladderDirection);            //vector3_6 = LadderDirection

                Quaternion rotation = Quaternion.identity;
                rotation *= Quaternion.AngleAxis(eva.turnRate * state.pitch * 57.29578f * Time.deltaTime, -eva.transform.right);
                rotation *= Quaternion.AngleAxis(eva.turnRate * state.yaw * 57.29578f * Time.deltaTime, eva.transform.up);
                rotation *= Quaternion.AngleAxis(eva.turnRate * state.roll * 57.29578f * Time.deltaTime, -eva.transform.forward);
                if (rotation != Quaternion.identity)
                {
                    this.vectorFields[8].SetValue(eva, rotation * (Vector3)this.vectorFields[8].GetValue(eva));
                    this.vectorFields[13].SetValue(eva, rotation * (Vector3)this.vectorFields[13].GetValue(eva));
                }

                if (!moveDirection.IsZero() && !eva.OnALadder)
                {
                    if (eva.CharacterFrameMode)
                    {
                        this.vectorFields[8].SetValue(eva, eva.fFwd);           //vector3_8
                        this.vectorFields[13].SetValue(eva, eva.fUp);           //vector3_13
                    }
                    else
                    {
                        this.vectorFields[8].SetValue(eva, moveDirection);      //vector3_8
                        this.vectorFields[13].SetValue(eva, eva.fUp);           //vector3_13
                    }
                }
            }
            else
            {
                ReEnableStopConditions(eva);
            }
        }

        // We disable "Ladder Stop" check because it triggers on ladderDirection.isZero, which is zeroed by Squad.
        private void DisableLadderStopCondition(KerbalEVA eva)
        {
            KFSMEvent eLadderStop = (KFSMEvent)eventFields[26].GetValue(eva); // Ladder Stop
            if (this.ladderStopConditionDelegate == null)
            {
                this.ladderStopConditionDelegate = eLadderStop.OnCheckCondition;
            }
            //Debug.LogWarning("Disabling LadderStop");
            eLadderStop.OnCheckCondition = this.eventConditionDisabled;
        }

        private void DisableSwimStopCondition(KerbalEVA eva)
        {
            KFSMEvent eSwimStop = (KFSMEvent)eventFields[21].GetValue(eva); // Swim Stop
            if (this.swimStopConditionDelegate == null)
            {
                this.swimStopConditionDelegate = eSwimStop.OnCheckCondition;
            }
            //Debug.LogWarning("Disabling SwimStop");
            eSwimStop.OnCheckCondition = this.eventConditionDisabled;
        }

        private void ReEnableStopConditions(KerbalEVA eva)
        {
            if (this.ladderStopConditionDelegate != null)
            {
                //Debug.LogWarning("Re-enable LadderStop");
                KFSMEvent eLadderStop = (KFSMEvent)eventFields[26].GetValue(eva);
                eLadderStop.OnCheckCondition = this.ladderStopConditionDelegate;
                this.ladderStopConditionDelegate = null;
            }
            if (this.swimStopConditionDelegate != null)
            {
                //Debug.LogWarning("Re-enable SwimStop");
                KFSMEvent eSwimStop = (KFSMEvent)eventFields[21].GetValue(eva);
                eSwimStop.OnCheckCondition = this.swimStopConditionDelegate;
                this.swimStopConditionDelegate = null;
            }
        }

        public void DoInteract()
        {
            if (!FlightGlobals.ActiveVessel.isEVA)
                return;

            KerbalEVA eva = GetKerbalEVA();
            if (this.GetEVAColliders(eva).Count > 0)
            {
                if (eva.OnALadder)
                {
                    //TODO NullReference when not close to a hatch
                    eva.fsm.RunEvent((KFSMEvent)this.eventFields[34].GetValue(eva)); // Board Vessel
                }
                else
                {
                    eva.fsm.RunEvent((KFSMEvent)this.eventFields[22].GetValue(eva)); // Grab Ladder
                }
            }
        }

        public void DoJump()
        {
            if (!FlightGlobals.ActiveVessel.isEVA)
                return;

            KerbalEVA eva = GetKerbalEVA();
            if (eva.OnALadder)
            {
                eva.fsm.RunEvent((KFSMEvent)this.eventFields[27].GetValue(eva)); // Ladder Let Go
            }
            else
            {
                eva.fsm.RunEvent((KFSMEvent)this.eventFields[9].GetValue(eva)); // Jump Start
            }
        }

        public void DoPlantFlag()
        {
            if (!FlightGlobals.ActiveVessel.isEVA)
                return;

            KerbalEVA eva = GetKerbalEVA();
            eva.PlantFlag();
        }

        // Animation is bugged
        public void ToggleJetpack()
        {
            if (!FlightGlobals.ActiveVessel.isEVA)
                return;

            KerbalEVA eva = GetKerbalEVA();
            eva.JetpackDeployed = !eva.JetpackDeployed;
            
            // Possible solutions to animation problem:
            //MethodInfo method = typeof(KerbalEVA).GetMethod("ToggleJetpack", BindingFlags.Instance | BindingFlags.NonPublic);
            //method.Invoke(eva, new object[] { !eva.JetpackDeployed });
            //eva.fsm.RunEvent((KFSMEvent)eventFields[17].GetValue(eva));
        }

        public void SetMoveSpeed(float runSpeed)
        {
            if (!FlightGlobals.ActiveVessel.isEVA)
                return;

            KerbalEVA eva = GetKerbalEVA();
            if (IsVesselActive(eva.vessel) 
                && !eva.JetpackDeployed && !eva.OnALadder && !eva.isRagdoll)
            {
                eva.animation.CrossFade( GetAnimationForVelocity(eva), 0.2f );
                eva.animation.Blend(eva.Animations.walkLowGee, Mathf.InverseLerp(1f, eva.minWalkingGee, (float)eva.vessel.mainBody.GeeASL));
                eva.Animations.walkLowGee.State.speed = 2.7f;
                floatFields[6].SetValue(eva, eva.runSpeed * runSpeed);
            }
        }

        public void ToggleHeadlamp()
        {
            if (!FlightGlobals.ActiveVessel.isEVA)
                return;

            KerbalEVA eva = GetKerbalEVA();
            eva.headLamp.SetActive(!eva.lampOn);
            eva.lampOn = !eva.lampOn;
        }

        public void ToggleAutorun()
        {
            this.m_autoRunning = !this.m_autoRunning;
            Debug.Log("EVA AutoRun: "+ this.m_autoRunning);
        }

        // PRIVATE FIELDS //

        private KerbalAnimationState GetAnimationForVelocity(KerbalEVA eva)
        {
            float srfVelocity = eva.vessel.GetSrfVelocity().magnitude;
            if (srfVelocity > 1.75f) return eva.Animations.run;
            else if (srfVelocity < 0.05) return eva.Animations.turnRight;
            else return eva.Animations.walkFwd;
        }

        private List<Collider> GetEVAColliders(KerbalEVA eva)
        {
            return (List<Collider>)this.colliderListField.GetValue(eva);
        }

        private bool IsVesselActive(Vessel vessel)
        {
            return vessel.state == Vessel.State.ACTIVE && !vessel.packed;
        }

        private KerbalEVA GetKerbalEVA()
        {
            return FlightGlobals.ActiveVessel.GetComponent<KerbalEVA>();
        }

        private void LoadReflectionFields()
        {
            List<FieldInfo> fields = new List<FieldInfo>(typeof(KerbalEVA).GetFields(
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
            this.vectorFields = new List<FieldInfo>(fields.Where<FieldInfo>(f => f.FieldType.Equals(typeof(Vector3))));
            this.floatFields = new List<FieldInfo>(fields.Where<FieldInfo>(f => f.FieldType.Equals(typeof(float))));
            this.eventFields = new List<FieldInfo>(fields.Where<FieldInfo>(f => f.FieldType.Equals(typeof(KFSMEvent))));
            this.colliderListField = new List<FieldInfo>(fields.Where<FieldInfo>(f => f.FieldType.Equals(typeof(List<Collider>))))[0];
        }
    }
}
