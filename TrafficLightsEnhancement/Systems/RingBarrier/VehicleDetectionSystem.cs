using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Game.Net;
using Game.Common;
using Game.Vehicles;
using Game.Objects;
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
    private EntityQuery m_LaneQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        m_DetectorQuery = SystemAPI.QueryBuilder()
            .WithAll<DetectorData>()
            .Build();

        m_VehicleQuery = SystemAPI.QueryBuilder()
            .WithAll<Vehicle, Moving>()
            .WithAll<Target>()
            .Build();

        m_ControllerQuery = SystemAPI.QueryBuilder()
            .WithAll<RingBarrierController, CallData>()
            .WithAll<CustomTrafficLights>()
            .Build();

        m_LaneQuery = SystemAPI.QueryBuilder()
            .WithAll<Lane, Curve>()
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
            TargetLookup = SystemAPI.GetComponentLookup<Target>(true),
            MovingLookup = SystemAPI.GetComponentLookup<Moving>(true),
            TransformLookup = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
            LaneLookup = SystemAPI.GetComponentLookup<Lane>(true),
            CurveLookup = SystemAPI.GetComponentLookup<Curve>(true),
            LaneObjectsLookup = SystemAPI.GetBufferLookup<LaneObject>(true)
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
        public ComponentLookup<Target> TargetLookup;
        
        [ReadOnly]
        public ComponentLookup<Moving> MovingLookup;
        
        [ReadOnly]
        public ComponentLookup<Game.Objects.Transform> TransformLookup;
        
        [ReadOnly]
        public ComponentLookup<Lane> LaneLookup;
        
        [ReadOnly]
        public ComponentLookup<Curve> CurveLookup;
        
        [ReadOnly]
        public BufferLookup<LaneObject> LaneObjectsLookup;

        public void Execute(ref DynamicBuffer<DetectorData> detectors)
        {
            for (int i = 0; i < detectors.Length; i++)
            {
                var detector = detectors[i];
                if (!detector.m_Enabled || detector.m_LaneEntity == Entity.Null)
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
            // Check if lane exists and has vehicles
            if (!LaneObjectsLookup.HasBuffer(detector.m_LaneEntity))
                return false;

            var laneObjects = LaneObjectsLookup[detector.m_LaneEntity];
            if (!CurveLookup.TryGetComponent(detector.m_LaneEntity, out var laneCurve))
                return false;

            var detectionZone = detector.GetDetectionZone();
            
            // Check each vehicle on the lane
            for (int i = 0; i < laneObjects.Length; i++)
            {
                var laneObject = laneObjects[i];
                
                // Only check vehicles
                if (!VehicleLookup.HasComponent(laneObject.m_LaneObject))
                    continue;

                // Get vehicle position on lane (0.0 to 1.0)
                float vehiclePosition = GetVehiclePositionOnLane(laneObject, laneCurve);
                
                // Check if vehicle is in detection zone
                if (vehiclePosition >= detectionZone.x && vehiclePosition <= detectionZone.y)
                {
                    // Apply sensitivity check
                    if (UnityEngine.Random.Range(0f, 1f) < detector.m_Sensitivity)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private float GetVehiclePositionOnLane(LaneObject laneObject, Curve laneCurve)
        {
            // Simplified position calculation
            // In a real implementation, this would use the vehicle's transform
            // and project it onto the lane curve to get the exact position
            
            // For now, use a approximation based on curve parameter
            return math.clamp(laneObject.m_CurvePosition, 0f, 1f);
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