using Colossal.Serialization.Entities;
using Unity.Entities;
using Unity.Mathematics;

namespace C2VM.TrafficLightsEnhancement.Components;

/// <summary>
/// Vehicle detector data for lane-based detection
/// </summary>
public struct DetectorData : IBufferElementData, ISerializable
{
    public enum DetectorType : byte
    {
        /// <summary>Presence detector - detects continuous occupancy</summary>
        Presence = 0,
        /// <summary>Pulse detector - sends pulse when vehicle passes</summary>
        Pulse = 1,
        /// <summary>Speed detector - measures vehicle speed</summary>
        Speed = 2,
        /// <summary>Queue detector - detects queued vehicles</summary>
        Queue = 3
    }

    public enum DetectorState : byte
    {
        /// <summary>No vehicle detected</summary>
        Clear = 0,
        /// <summary>Vehicle detected and active</summary>
        Occupied = 1,
        /// <summary>Recently cleared (for pulse detection)</summary>
        RecentlyClear = 2,
        /// <summary>Detector is malfunctioning</summary>
        Fault = 3
    }

    private ushort m_SchemaVersion;

    /// <summary>Unique detector ID</summary>
    public ushort m_DetectorId;

    /// <summary>Phase this detector is assigned to</summary>
    public byte m_AssignedPhase;

    /// <summary>Lane entity this detector monitors</summary>
    public Entity m_LaneEntity;

    /// <summary>Type of detector</summary>
    public DetectorType m_Type;

    /// <summary>Current state of detector</summary>
    public DetectorState m_State;

    /// <summary>Detection zone position along lane (0.0 to 1.0)</summary>
    public float m_Position;

    /// <summary>Detection zone length in meters</summary>
    public float m_Length;

    /// <summary>Detection sensitivity (0.0 to 1.0)</summary>
    public float m_Sensitivity;

    /// <summary>Extension time for presence detectors in deciseconds</summary>
    public ushort m_ExtensionTime;

    /// <summary>Maximum extension limit in deciseconds</summary>
    public ushort m_MaxExtension;

    /// <summary>Current occupancy time in deciseconds</summary>
    public ushort m_OccupancyTime;

    /// <summary>Call placed timestamp</summary>
    public uint m_CallPlacedTime;

    /// <summary>Whether this detector can place calls</summary>
    public bool m_CanPlaceCalls;

    /// <summary>Whether this detector provides extension</summary>
    public bool m_ProvidesExtension;

    /// <summary>Whether detector is enabled</summary>
    public bool m_Enabled;

    /// <summary>Detection statistics - vehicle count</summary>
    public ushort m_VehicleCount;

    /// <summary>Average speed detected (km/h * 10)</summary>
    public ushort m_AverageSpeed;

    public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
    {
        writer.Write(m_SchemaVersion);
        writer.Write(m_DetectorId);
        writer.Write(m_AssignedPhase);
        writer.Write(m_LaneEntity);
        writer.Write((byte)m_Type);
        writer.Write((byte)m_State);
        writer.Write(m_Position);
        writer.Write(m_Length);
        writer.Write(m_Sensitivity);
        writer.Write(m_ExtensionTime);
        writer.Write(m_MaxExtension);
        writer.Write(m_OccupancyTime);
        writer.Write(m_CallPlacedTime);
        writer.Write(m_CanPlaceCalls);
        writer.Write(m_ProvidesExtension);
        writer.Write(m_Enabled);
        writer.Write(m_VehicleCount);
        writer.Write(m_AverageSpeed);
    }

    public void Deserialize<TReader>(TReader reader) where TReader : IReader
    {
        Initialization();
        reader.Read(out m_SchemaVersion);
        reader.Read(out m_DetectorId);
        reader.Read(out m_AssignedPhase);
        reader.Read(out m_LaneEntity);
        reader.Read(out byte type);
        reader.Read(out byte state);
        reader.Read(out m_Position);
        reader.Read(out m_Length);
        reader.Read(out m_Sensitivity);
        reader.Read(out m_ExtensionTime);
        reader.Read(out m_MaxExtension);
        reader.Read(out m_OccupancyTime);
        reader.Read(out m_CallPlacedTime);
        reader.Read(out m_CanPlaceCalls);
        reader.Read(out m_ProvidesExtension);
        reader.Read(out m_Enabled);
        reader.Read(out m_VehicleCount);
        reader.Read(out m_AverageSpeed);
        m_Type = (DetectorType)type;
        m_State = (DetectorState)state;
    }

    private void Initialization()
    {
        m_SchemaVersion = 1;
        m_DetectorId = 0;
        m_AssignedPhase = 1;
        m_LaneEntity = Entity.Null;
        m_Type = DetectorType.Presence;
        m_State = DetectorState.Clear;
        m_Position = 0.9f; // Near stop line
        m_Length = 2.0f; // 2 meter detection zone
        m_Sensitivity = 0.8f;
        m_ExtensionTime = 30; // 3 seconds
        m_MaxExtension = 50; // 5 seconds
        m_OccupancyTime = 0;
        m_CallPlacedTime = 0;
        m_CanPlaceCalls = true;
        m_ProvidesExtension = true;
        m_Enabled = true;
        m_VehicleCount = 0;
        m_AverageSpeed = 500; // 50 km/h default
    }

    public DetectorData()
    {
        Initialization();
    }

    /// <summary>
    /// Updates detector state based on vehicle presence
    /// </summary>
    public void UpdateState(bool vehiclePresent, uint currentTime)
    {
        DetectorState previousState = m_State;

        if (vehiclePresent)
        {
            if (m_State == DetectorState.Clear || m_State == DetectorState.RecentlyClear)
            {
                m_State = DetectorState.Occupied;
                m_OccupancyTime = 0;
                m_VehicleCount++;
            }
            else if (m_State == DetectorState.Occupied)
            {
                m_OccupancyTime++;
            }
        }
        else
        {
            if (m_State == DetectorState.Occupied)
            {
                m_State = m_Type == DetectorType.Pulse ? DetectorState.RecentlyClear : DetectorState.Clear;
                m_OccupancyTime = 0;
            }
            else if (m_State == DetectorState.RecentlyClear)
            {
                m_State = DetectorState.Clear;
            }
        }
    }

    /// <summary>
    /// Checks if detector should place a call
    /// </summary>
    public readonly bool ShouldPlaceCall()
    {
        return m_Enabled && m_CanPlaceCalls && 
               (m_State == DetectorState.Occupied || 
                (m_Type == DetectorType.Pulse && m_State == DetectorState.RecentlyClear));
    }

    /// <summary>
    /// Checks if detector should provide extension time
    /// </summary>
    public readonly bool ShouldProvideExtension()
    {
        return m_Enabled && m_ProvidesExtension && 
               m_State == DetectorState.Occupied &&
               m_OccupancyTime <= m_MaxExtension;
    }

    /// <summary>
    /// Gets the detection zone bounds
    /// </summary>
    public readonly float2 GetDetectionZone()
    {
        float start = math.max(0f, m_Position - m_Length * 0.5f);
        float end = math.min(1f, m_Position + m_Length * 0.5f);
        return new float2(start, end);
    }
}