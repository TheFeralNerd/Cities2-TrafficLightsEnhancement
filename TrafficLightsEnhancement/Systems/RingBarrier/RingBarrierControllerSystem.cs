using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using C2VM.TrafficLightsEnhancement.Components;

namespace C2VM.TrafficLightsEnhancement.Systems.RingBarrier;

/// <summary>
/// Core system that handles ring & barrier logic for traffic signal controllers
/// </summary>
[BurstCompile]
public partial struct RingBarrierControllerSystem : ISystem
{
    private EntityQuery m_RingBarrierQuery;
    private EntityQuery m_DetectorQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        m_RingBarrierQuery = SystemAPI.QueryBuilder()
            .WithAll<RingBarrierController, PhaseDefinition, CallData>()
            .WithAll<CustomTrafficLights>()
            .Build();

        m_DetectorQuery = SystemAPI.QueryBuilder()
            .WithAll<DetectorData>()
            .Build();

        state.RequireForUpdate(m_RingBarrierQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var job = new RingBarrierUpdateJob
        {
            CurrentTime = (uint)SystemAPI.Time.ElapsedTime,
            DeltaTime = SystemAPI.Time.DeltaTime
        };

        state.Dependency = job.ScheduleParallel(m_RingBarrierQuery, state.Dependency);
    }

    [BurstCompile]
    private partial struct RingBarrierUpdateJob : IJobEntity
    {
        public uint CurrentTime;
        public float DeltaTime;

        public void Execute(
            ref RingBarrierController controller,
            ref DynamicBuffer<PhaseDefinition> phases,
            ref DynamicBuffer<CallData> calls,
            in CustomTrafficLights trafficLights)
        {
            // Only process if using ring & barrier pattern
            if (trafficLights.GetPatternOnly() != CustomTrafficLights.Patterns.RingBarrier)
                return;

            // Update cycle timer
            controller.m_CycleTimer = (ushort)((controller.m_CycleTimer + (DeltaTime * 10)) % controller.m_CycleLength);

            // Process calls and update phase states
            ProcessCalls(ref calls, ref phases, CurrentTime);
            
            // Update phase timings
            UpdatePhaseTimings(ref controller, ref phases, CurrentTime);
            
            // Handle ring & barrier logic
            ProcessRingBarrierLogic(ref controller, ref phases, CurrentTime);
            
            // Clean up expired calls
            CleanupCalls(ref calls, CurrentTime);
        }

        private void ProcessCalls(ref DynamicBuffer<CallData> calls, ref DynamicBuffer<PhaseDefinition> phases, uint currentTime)
        {
            // Mark phases with active calls
            for (int i = 0; i < phases.Length; i++)
            {
                var phase = phases[i];
                phase.m_HasCalls = false;
                
                for (int j = 0; j < calls.Length; j++)
                {
                    var call = calls[j];
                    if (call.m_Phase == phase.m_PhaseNumber && call.IsActive())
                    {
                        phase.m_HasCalls = true;
                        break;
                    }
                }
                
                phases[i] = phase;
            }
        }

        private void UpdatePhaseTimings(ref RingBarrierController controller, ref DynamicBuffer<PhaseDefinition> phases, uint currentTime)
        {
            for (int i = 0; i < phases.Length; i++)
            {
                var phase = phases[i];
                if (!phase.m_Enabled)
                    continue;

                bool isActive = controller.IsPhaseActive(phase.m_PhaseNumber);
                
                if (isActive)
                {
                    phase.m_CurrentTiming++;
                    
                    // Update phase state based on timing
                    switch (phase.m_State)
                    {
                        case PhaseDefinition.PhaseState.MinimumGreen:
                            if (phase.m_CurrentTiming >= phase.m_MinimumGreen)
                            {
                                if (phase.m_HasCalls && HasExtensionCall(phase.m_PhaseNumber, phases))
                                {
                                    phase.m_State = PhaseDefinition.PhaseState.PassageTime;
                                    phase.m_CurrentTiming = 0;
                                }
                                else if (phase.m_CurrentTiming >= phase.m_MaximumGreen)
                                {
                                    phase.m_State = PhaseDefinition.PhaseState.Yellow;
                                    phase.m_CurrentTiming = 0;
                                }
                            }
                            break;
                            
                        case PhaseDefinition.PhaseState.PassageTime:
                            if (phase.m_CurrentTiming >= phase.m_PassageTime || !HasExtensionCall(phase.m_PhaseNumber, phases))
                            {
                                phase.m_State = PhaseDefinition.PhaseState.Yellow;
                                phase.m_CurrentTiming = 0;
                            }
                            break;
                            
                        case PhaseDefinition.PhaseState.MaximumGreen:
                            phase.m_State = PhaseDefinition.PhaseState.Yellow;
                            phase.m_CurrentTiming = 0;
                            break;
                            
                        case PhaseDefinition.PhaseState.Yellow:
                            if (phase.m_CurrentTiming >= phase.m_YellowTime)
                            {
                                phase.m_State = PhaseDefinition.PhaseState.AllRed;
                                phase.m_CurrentTiming = 0;
                            }
                            break;
                            
                        case PhaseDefinition.PhaseState.AllRed:
                            if (phase.m_CurrentTiming >= phase.m_AllRedTime)
                            {
                                // Phase complete - can advance to next
                                controller.SetPhaseActive(phase.m_PhaseNumber, false);
                                phase.m_State = PhaseDefinition.PhaseState.Rest;
                                phase.m_CurrentTiming = 0;
                            }
                            break;
                    }
                }
                else if (phase.m_HasCalls && phase.m_State == PhaseDefinition.PhaseState.Rest)
                {
                    phase.m_State = PhaseDefinition.PhaseState.CallsPresent;
                }
                
                phases[i] = phase;
            }
        }

        private void ProcessRingBarrierLogic(ref RingBarrierController controller, ref DynamicBuffer<PhaseDefinition> phases, uint currentTime)
        {
            // Check if we can start new phases
            for (byte ring = 0; ring < controller.m_RingCount; ring++)
            {
                if (!IsRingActive(controller, phases, ring))
                {
                    // Ring is idle, can start next phase
                    byte nextPhase = GetNextPhaseForRing(controller, phases, ring);
                    if (nextPhase != 0 && CanStartPhase(controller, phases, nextPhase))
                    {
                        StartPhase(ref controller, ref phases, nextPhase);
                    }
                }
            }
        }

        private bool IsRingActive(RingBarrierController controller, DynamicBuffer<PhaseDefinition> phases, byte ring)
        {
            for (int i = 0; i < phases.Length; i++)
            {
                var phase = phases[i];
                if (phase.m_Ring == ring && controller.IsPhaseActive(phase.m_PhaseNumber))
                {
                    return true;
                }
            }
            return false;
        }

        private byte GetNextPhaseForRing(RingBarrierController controller, DynamicBuffer<PhaseDefinition> phases, byte ring)
        {
            // Simple sequential logic - find next phase with calls in ring
            for (int i = 0; i < phases.Length; i++)
            {
                var phase = phases[i];
                if (phase.m_Ring == ring && phase.m_HasCalls && phase.m_Enabled && 
                    phase.m_State == PhaseDefinition.PhaseState.CallsPresent)
                {
                    return phase.m_PhaseNumber;
                }
            }
            return 0;
        }

        private bool CanStartPhase(RingBarrierController controller, DynamicBuffer<PhaseDefinition> phases, byte phaseNumber)
        {
            // Check for conflicts
            for (int i = 0; i < phases.Length; i++)
            {
                var phase = phases[i];
                if (controller.IsPhaseActive(phase.m_PhaseNumber) && phase.ConflictsWith(phaseNumber))
                {
                    return false;
                }
            }
            return true;
        }

        private void StartPhase(ref RingBarrierController controller, ref DynamicBuffer<PhaseDefinition> phases, byte phaseNumber)
        {
            for (int i = 0; i < phases.Length; i++)
            {
                var phase = phases[i];
                if (phase.m_PhaseNumber == phaseNumber)
                {
                    controller.SetPhaseActive(phaseNumber, true);
                    phase.m_State = PhaseDefinition.PhaseState.MinimumGreen;
                    phase.m_CurrentTiming = 0;
                    phases[i] = phase;
                    break;
                }
            }
        }

        private bool HasExtensionCall(byte phaseNumber, DynamicBuffer<PhaseDefinition> phases)
        {
            // Simplified - in real implementation would check detector extension
            return false;
        }

        private void CleanupCalls(ref DynamicBuffer<CallData> calls, uint currentTime)
        {
            for (int i = calls.Length - 1; i >= 0; i--)
            {
                var call = calls[i];
                if (call.CheckTimeout(currentTime) || call.m_Status == CallData.CallStatus.Cleared)
                {
                    calls.RemoveAt(i);
                }
            }
        }
    }
}