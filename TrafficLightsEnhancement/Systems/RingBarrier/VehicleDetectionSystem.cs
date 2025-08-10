using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Game.Net;
using Game.Common;
using Game.Vehicles;
using C2VM.TrafficLightsEnhancement.Components;

namespace C2VM.TrafficLightsEnhancement.Systems.RingBarrier;

/// <summary>
/// System that detects vehicles in lanes and places calls for service
/// </summary>
[BurstCompile]
public partial struct VehicleDetectionSystem : ISystem
{
    private EntityQuery m_DetectorQuery;
    private EntityQuery m_VehicleQuery;
    private EntityQuery m_ControllerQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        m_DetectorQuery = SystemAPI.QueryBuilder()
            .WithAll<DetectorData>()
            .Build();

        m_VehicleQuery = SystemAPI.QueryBuilder()
            .WithAll<Vehicle, Game.Objects.Moving>()
            .WithAll<Game.Common.Target>()
            .Build();

        m_ControllerQuery = SystemAPI.QueryBuilder()
            .WithAll<RingBarrierController, CallData>()
            .WithAll<CustomTrafficLights>()
            .Build();

        state.RequireForUpdate(m_DetectorQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        uint currentTime = (uint)(SystemAPI.Time.ElapsedTime * 10); // Convert to deciseconds

        var detectorJob = new VehicleDetectionJob
        {
            CurrentTime = currentTime,
            VehicleLookup = SystemAPI.GetComponentLookup<Vehicle>(true),
            TargetLookup = SystemAPI.GetComponentLookup<Game.Common.Target>(true),
            MovingLookup = SystemAPI.GetComponentLookup<Game.Objects.Moving>(true)
        };

        var callPlacementJob = new CallPlacementJob
        {
            CurrentTime = currentTime
        };

        state.Dependency = detectorJob.ScheduleParallel(m_DetectorQuery, state.Dependency);
        state.Dependency = callPlacementJob.ScheduleParallel(m_ControllerQuery, state.Dependency);
    }

    [BurstCompile]
    private partial struct VehicleDetectionJob : IJobEntity
    {
        public uint CurrentTime;
        
        [ReadOnly]
        public ComponentLookup<Vehicle> VehicleLookup;
        
        [ReadOnly]
        public ComponentLookup<Game.Common.Target> TargetLookup;
        
        [ReadOnly]
        public ComponentLookup<Game.Objects.Moving> MovingLookup;

        public void Execute(ref DynamicBuffer<DetectorData> detectors)
        {
            for (int i = 0; i < detectors.Length; i++)
            {
                var detector = detectors[i];
                if (!detector.m_Enabled)
                    continue;

                // Check for vehicle presence in detection zone
                bool vehicleDetected = CheckVehiclePresence(detector);
                
                // Update detector state
                detector.UpdateState(vehicleDetected, CurrentTime);
                
                detectors[i] = detector;
            }
        }

        private bool CheckVehiclePresence(DetectorData detector)
        {
            // Simplified detection logic
            // In a real implementation, this would:
            // 1. Get all vehicles on the detector's lane
            // 2. Check if any vehicle is within the detection zone
            // 3. Apply sensitivity and filtering
            
            // For now, return a simple simulation based on detector position
            // This would be replaced with actual vehicle position checking
            return UnityEngine.Random.Range(0f, 1f) < 0.1f; // 10% chance of detection per frame
        }
    }

    [BurstCompile]
    private partial struct CallPlacementJob : IJobEntity
    {
        public uint CurrentTime;

        public void Execute(
            ref DynamicBuffer<CallData> calls,
            in DynamicBuffer<DetectorData> detectors,
            in RingBarrierController controller,
            in CustomTrafficLights trafficLights)
        {
            // Only process if using ring & barrier pattern
            if (trafficLights.GetPatternOnly() != CustomTrafficLights.Patterns.RingBarrier)
                return;

            for (int i = 0; i < detectors.Length; i++)
            {
                var detector = detectors[i];
                
                if (detector.ShouldPlaceCall())
                {
                    // Check if call already exists for this detector/phase
                    bool callExists = false;
                    for (int j = 0; j < calls.Length; j++)
                    {
                        var existingCall = calls[j];
                        if (existingCall.m_DetectorId == detector.m_DetectorId && 
                            existingCall.m_Phase == detector.m_AssignedPhase &&
                            existingCall.IsActive())
                        {
                            callExists = true;
                            break;
                        }
                    }

                    if (!callExists)
                    {
                        // Place new call
                        var newCall = new CallData(
                            detector.m_AssignedPhase, 
                            CallData.CallType.Vehicular, 
                            detector.m_DetectorId, 
                            CurrentTime);
                        
                        calls.Add(newCall);
                    }
                }
            }
        }
    }
}