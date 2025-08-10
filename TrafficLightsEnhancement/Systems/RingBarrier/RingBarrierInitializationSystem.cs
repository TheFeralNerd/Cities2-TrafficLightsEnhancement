using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Game.Net;
using C2VM.TrafficLightsEnhancement.Components;

namespace C2VM.TrafficLightsEnhancement.Systems.RingBarrier;

/// <summary>
/// System that initializes ring & barrier components when the pattern is selected
/// </summary>
[BurstCompile]
public partial struct RingBarrierInitializationSystem : ISystem
{
    private EntityQuery m_TrafficLightQuery;
    private EntityCommandBuffer.ParallelWriter m_CommandBuffer;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        m_TrafficLightQuery = SystemAPI.QueryBuilder()
            .WithAll<CustomTrafficLights>()
            .WithAll<Game.Net.TrafficLights>()
            .Build();

        state.RequireForUpdate(m_TrafficLightQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        
        var job = new InitializationJob
        {
            CommandBuffer = ecb.AsParallelWriter(),
            RingBarrierLookup = SystemAPI.GetComponentLookup<RingBarrierController>(false),
            PhaseDefLookup = SystemAPI.GetBufferLookup<PhaseDefinition>(false),
            DetectorLookup = SystemAPI.GetBufferLookup<DetectorData>(false),
            CallLookup = SystemAPI.GetBufferLookup<CallData>(false),
            TimingParamsLookup = SystemAPI.GetComponentLookup<TimingParameters>(false)
        };

        state.Dependency = job.ScheduleParallel(m_TrafficLightQuery, state.Dependency);
    }

    [BurstCompile]
    private partial struct InitializationJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter CommandBuffer;
        
        public ComponentLookup<RingBarrierController> RingBarrierLookup;
        public BufferLookup<PhaseDefinition> PhaseDefLookup;
        public BufferLookup<DetectorData> DetectorLookup;
        public BufferLookup<CallData> CallLookup;
        public ComponentLookup<TimingParameters> TimingParamsLookup;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in CustomTrafficLights trafficLights)
        {
            // Check if ring & barrier pattern is selected
            if (trafficLights.GetPatternOnly() != CustomTrafficLights.Patterns.RingBarrier)
            {
                // Remove ring & barrier components if pattern changed
                if (RingBarrierLookup.HasComponent(entity))
                {
                    CommandBuffer.RemoveComponent<RingBarrierController>(chunkIndex, entity);
                    CommandBuffer.RemoveBuffer<PhaseDefinition>(chunkIndex, entity);
                    CommandBuffer.RemoveBuffer<DetectorData>(chunkIndex, entity);
                    CommandBuffer.RemoveBuffer<CallData>(chunkIndex, entity);
                    CommandBuffer.RemoveComponent<TimingParameters>(chunkIndex, entity);
                }
                return;
            }

            // Initialize ring & barrier components if not present
            if (!RingBarrierLookup.HasComponent(entity))
            {
                // Add ring & barrier controller
                CommandBuffer.AddComponent(chunkIndex, entity, new RingBarrierController());
                
                // Add timing parameters
                CommandBuffer.AddComponent(chunkIndex, entity, new TimingParameters());
                
                // Add phase definition buffer
                var phaseBuffer = CommandBuffer.AddBuffer<PhaseDefinition>(chunkIndex, entity);
                InitializeStandardPhases(ref phaseBuffer);
                
                // Add detector buffer
                var detectorBuffer = CommandBuffer.AddBuffer<DetectorData>(chunkIndex, entity);
                // Detector initialization will be handled by a separate system
                
                // Add call buffer
                CommandBuffer.AddBuffer<CallData>(chunkIndex, entity);
            }
        }

        private void InitializeStandardPhases(ref DynamicBuffer<PhaseDefinition> phases)
        {
            // Standard 8-phase intersection with dual ring configuration
            
            // Ring 1: Phases 1, 2, 3, 4
            phases.Add(CreatePhase(1, 0, PhaseDefinition.PhaseType.Vehicular)); // EB/WB Through
            phases.Add(CreatePhase(2, 0, PhaseDefinition.PhaseType.ProtectedTurn)); // EB/WB Left
            phases.Add(CreatePhase(3, 0, PhaseDefinition.PhaseType.Vehicular)); // NB/SB Through
            phases.Add(CreatePhase(4, 0, PhaseDefinition.PhaseType.ProtectedTurn)); // NB/SB Left
            
            // Ring 2: Phases 5, 6, 7, 8 (typically pedestrian or overlap phases)
            phases.Add(CreatePhase(5, 1, PhaseDefinition.PhaseType.Pedestrian)); // EB/WB Pedestrian
            phases.Add(CreatePhase(6, 1, PhaseDefinition.PhaseType.Pedestrian)); // EB/WB Pedestrian
            phases.Add(CreatePhase(7, 1, PhaseDefinition.PhaseType.Pedestrian)); // NB/SB Pedestrian
            phases.Add(CreatePhase(8, 1, PhaseDefinition.PhaseType.Pedestrian)); // NB/SB Pedestrian

            // Set up standard conflicts
            SetupStandardConflicts(ref phases);
        }

        private PhaseDefinition CreatePhase(byte number, byte ring, PhaseDefinition.PhaseType type)
        {
            var phase = new PhaseDefinition();
            phase.m_PhaseNumber = number;
            phase.m_Ring = ring;
            phase.m_Type = type;
            phase.m_State = PhaseDefinition.PhaseState.Rest;
            
            // Set timing based on phase type
            switch (type)
            {
                case PhaseDefinition.PhaseType.Vehicular:
                    phase.m_MinimumGreen = 100; // 10 seconds
                    phase.m_PassageTime = 30; // 3 seconds
                    phase.m_MaximumGreen = 600; // 60 seconds
                    phase.m_YellowTime = 40; // 4 seconds
                    phase.m_AllRedTime = 20; // 2 seconds
                    break;
                    
                case PhaseDefinition.PhaseType.ProtectedTurn:
                    phase.m_MinimumGreen = 70; // 7 seconds
                    phase.m_PassageTime = 25; // 2.5 seconds
                    phase.m_MaximumGreen = 300; // 30 seconds
                    phase.m_YellowTime = 35; // 3.5 seconds
                    phase.m_AllRedTime = 15; // 1.5 seconds
                    break;
                    
                case PhaseDefinition.PhaseType.Pedestrian:
                    phase.m_MinimumGreen = 150; // 15 seconds
                    phase.m_PassageTime = 0; // No passage time for pedestrians
                    phase.m_MaximumGreen = 300; // 30 seconds
                    phase.m_YellowTime = 40; // 4 seconds (clearance)
                    phase.m_AllRedTime = 20; // 2 seconds
                    phase.m_HasPedestrian = true;
                    break;
            }
            
            return phase;
        }

        private void SetupStandardConflicts(ref DynamicBuffer<PhaseDefinition> phases)
        {
            // Standard conflict matrix for 8-phase intersection
            // Phase 1 (EB/WB Through) conflicts with phases 3,4,7,8
            phases[0].SetConflict(3, true);
            phases[0].SetConflict(4, true);
            phases[0].SetConflict(7, true);
            phases[0].SetConflict(8, true);
            
            // Phase 2 (EB/WB Left) conflicts with phases 1,3,4,6,7,8
            phases[1].SetConflict(1, true);
            phases[1].SetConflict(3, true);
            phases[1].SetConflict(4, true);
            phases[1].SetConflict(6, true);
            phases[1].SetConflict(7, true);
            phases[1].SetConflict(8, true);
            
            // Phase 3 (NB/SB Through) conflicts with phases 1,2,5,6
            phases[2].SetConflict(1, true);
            phases[2].SetConflict(2, true);
            phases[2].SetConflict(5, true);
            phases[2].SetConflict(6, true);
            
            // Phase 4 (NB/SB Left) conflicts with phases 1,2,3,5,6,8
            phases[3].SetConflict(1, true);
            phases[3].SetConflict(2, true);
            phases[3].SetConflict(3, true);
            phases[3].SetConflict(5, true);
            phases[3].SetConflict(6, true);
            phases[3].SetConflict(8, true);
        }
    }
}