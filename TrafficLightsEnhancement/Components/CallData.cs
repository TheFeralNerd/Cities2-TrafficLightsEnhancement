using Colossal.Serialization.Entities;
using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Components;

/// <summary>
/// Represents a call for service placed by vehicle detectors
/// </summary>
public struct CallData : IBufferElementData, ISerializable
{
    public enum CallType : byte
    {
        /// <summary>Standard vehicular call</summary>
        Vehicular = 0,
        /// <summary>Pedestrian button call</summary>
        Pedestrian = 1,
        /// <summary>Emergency vehicle preemption call</summary>
        Emergency = 2,
        /// <summary>Railroad preemption call</summary>
        Railroad = 3,
        /// <summary>Transit priority call</summary>
        Transit = 4,
        /// <summary>Coordination call (force off)</summary>
        Coordination = 5
    }

    public enum CallPriority : byte
    {
        /// <summary>Normal priority call</summary>
        Normal = 0,
        /// <summary>High priority call (transit, emergency)</summary>
        High = 1,
        /// <summary>Maximum priority call (preemption)</summary>
        Maximum = 2
    }

    public enum CallStatus : byte
    {
        /// <summary>Call is active and waiting to be served</summary>
        Active = 0,
        /// <summary>Call is being served (phase is green)</summary>
        Served = 1,
        /// <summary>Call has been cleared/completed</summary>
        Cleared = 2,
        /// <summary>Call has timed out</summary>
        TimedOut = 3
    }

    private ushort m_SchemaVersion;

    /// <summary>Unique call identifier</summary>
    public uint m_CallId;

    /// <summary>Phase this call is for</summary>
    public byte m_Phase;

    /// <summary>Type of call</summary>
    public CallType m_Type;

    /// <summary>Priority level of call</summary>
    public CallPriority m_Priority;

    /// <summary>Current status of call</summary>
    public CallStatus m_Status;

    /// <summary>Detector that placed this call</summary>
    public ushort m_DetectorId;

    /// <summary>Time when call was placed (simulation ticks)</summary>
    public uint m_PlacedTime;

    /// <summary>Time when call was served</summary>
    public uint m_ServedTime;

    /// <summary>Time when call was cleared</summary>
    public uint m_ClearedTime;

    /// <summary>Maximum time call can remain active (0 = no timeout)</summary>
    public ushort m_TimeoutDuration;

    /// <summary>Whether call can be extended</summary>
    public bool m_Extendable;

    /// <summary>Whether call is persistent (doesn't clear after service)</summary>
    public bool m_Persistent;

    /// <summary>Whether call requires minimum green time</summary>
    public bool m_RequiresMinGreen;

    public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
    {
        writer.Write(m_SchemaVersion);
        writer.Write(m_CallId);
        writer.Write(m_Phase);
        writer.Write((byte)m_Type);
        writer.Write((byte)m_Priority);
        writer.Write((byte)m_Status);
        writer.Write(m_DetectorId);
        writer.Write(m_PlacedTime);
        writer.Write(m_ServedTime);
        writer.Write(m_ClearedTime);
        writer.Write(m_TimeoutDuration);
        writer.Write(m_Extendable);
        writer.Write(m_Persistent);
        writer.Write(m_RequiresMinGreen);
    }

    public void Deserialize<TReader>(TReader reader) where TReader : IReader
    {
        Initialization();
        reader.Read(out m_SchemaVersion);
        reader.Read(out m_CallId);
        reader.Read(out m_Phase);
        reader.Read(out byte type);
        reader.Read(out byte priority);
        reader.Read(out byte status);
        reader.Read(out m_DetectorId);
        reader.Read(out m_PlacedTime);
        reader.Read(out m_ServedTime);
        reader.Read(out m_ClearedTime);
        reader.Read(out m_TimeoutDuration);
        reader.Read(out m_Extendable);
        reader.Read(out m_Persistent);
        reader.Read(out m_RequiresMinGreen);
        m_Type = (CallType)type;
        m_Priority = (CallPriority)priority;
        m_Status = (CallStatus)status;
    }

    private void Initialization()
    {
        m_SchemaVersion = 1;
        m_CallId = 0;
        m_Phase = 1;
        m_Type = CallType.Vehicular;
        m_Priority = CallPriority.Normal;
        m_Status = CallStatus.Active;
        m_DetectorId = 0;
        m_PlacedTime = 0;
        m_ServedTime = 0;
        m_ClearedTime = 0;
        m_TimeoutDuration = 0;
        m_Extendable = true;
        m_Persistent = false;
        m_RequiresMinGreen = true;
    }

    public CallData()
    {
        Initialization();
    }

    /// <summary>
    /// Creates a new call
    /// </summary>
    public CallData(byte phase, CallType type, ushort detectorId, uint currentTime)
    {
        Initialization();
        m_Phase = phase;
        m_Type = type;
        m_DetectorId = detectorId;
        m_PlacedTime = currentTime;
        
        // Set priority based on type
        switch (type)
        {
            case CallType.Emergency:
            case CallType.Railroad:
                m_Priority = CallPriority.Maximum;
                m_TimeoutDuration = 0; // No timeout for preemption
                m_Persistent = true;
                break;
            case CallType.Transit:
                m_Priority = CallPriority.High;
                m_TimeoutDuration = 600; // 60 seconds timeout
                break;
            case CallType.Pedestrian:
                m_Priority = CallPriority.Normal;
                m_TimeoutDuration = 1800; // 3 minutes timeout
                m_Persistent = true; // Ped calls persist until served
                break;
            default:
                m_Priority = CallPriority.Normal;
                m_TimeoutDuration = 0;
                break;
        }
    }

    /// <summary>
    /// Marks call as being served
    /// </summary>
    public void Serve(uint currentTime)
    {
        if (m_Status == CallStatus.Active)
        {
            m_Status = CallStatus.Served;
            m_ServedTime = currentTime;
        }
    }

    /// <summary>
    /// Clears the call
    /// </summary>
    public void Clear(uint currentTime)
    {
        m_Status = CallStatus.Cleared;
        m_ClearedTime = currentTime;
    }

    /// <summary>
    /// Checks if call has timed out
    /// </summary>
    public bool CheckTimeout(uint currentTime)
    {
        if (m_TimeoutDuration > 0 && m_Status == CallStatus.Active)
        {
            if (currentTime - m_PlacedTime >= m_TimeoutDuration)
            {
                m_Status = CallStatus.TimedOut;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gets the age of the call in simulation ticks
    /// </summary>
    public readonly uint GetAge(uint currentTime)
    {
        return currentTime - m_PlacedTime;
    }

    /// <summary>
    /// Checks if call is still active
    /// </summary>
    public readonly bool IsActive()
    {
        return m_Status == CallStatus.Active || m_Status == CallStatus.Served;
    }

    /// <summary>
    /// Gets priority weight for call servicing logic
    /// </summary>
    public readonly float GetPriorityWeight(uint currentTime)
    {
        float baseWeight = (float)m_Priority;
        float ageWeight = GetAge(currentTime) * 0.001f; // Age factor
        return baseWeight + ageWeight;
    }
}