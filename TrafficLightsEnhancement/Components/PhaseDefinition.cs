using Colossal.Serialization.Entities;
using Unity.Entities;
using Unity.Mathematics;

namespace C2VM.TrafficLightsEnhancement.Components;

/// <summary>
/// Defines a traffic signal phase with real-world timing parameters
/// </summary>
public struct PhaseDefinition : IBufferElementData, ISerializable
{
    public enum PhaseState : byte
    {
        /// <summary>Phase is inactive, no calls present</summary>
        Rest = 0,
        /// <summary>Calls are present but phase is not yet green</summary>
        CallsPresent = 1,
        /// <summary>Phase is green, serving minimum green time</summary>
        MinimumGreen = 2,
        /// <summary>Phase is green, in passage time (gap extension)</summary>
        PassageTime = 3,
        /// <summary>Phase is green, at maximum green time</summary>
        MaximumGreen = 4,
        /// <summary>Phase is in yellow clearance</summary>
        Yellow = 5,
        /// <summary>Phase is in all-red clearance</summary>
        AllRed = 6,
        /// <summary>Phase is being held for coordination</summary>
        Hold = 7
    }

    public enum PhaseType : byte
    {
        /// <summary>Standard vehicular phase</summary>
        Vehicular = 0,
        /// <summary>Pedestrian-only phase</summary>
        Pedestrian = 1,
        /// <summary>Overlap phase (combination of other phases)</summary>
        Overlap = 2,
        /// <summary>Protected left turn phase</summary>
        ProtectedTurn = 3
    }

    private ushort m_SchemaVersion;

    /// <summary>Phase number (1-8 for standard intersections)</summary>
    public byte m_PhaseNumber;

    /// <summary>Ring this phase belongs to (0 or 1 for dual ring)</summary>
    public byte m_Ring;

    /// <summary>Type of phase</summary>
    public PhaseType m_Type;

    /// <summary>Current state of the phase</summary>
    public PhaseState m_State;

    /// <summary>Minimum green time in deciseconds</summary>
    public ushort m_MinimumGreen;

    /// <summary>Passage time (gap extension) in deciseconds</summary>
    public ushort m_PassageTime;

    /// <summary>Maximum green time in deciseconds</summary>
    public ushort m_MaximumGreen;

    /// <summary>Yellow clearance time in deciseconds</summary>
    public ushort m_YellowTime;

    /// <summary>All-red clearance time in deciseconds</summary>
    public ushort m_AllRedTime;

    /// <summary>Current timing value in deciseconds</summary>
    public ushort m_CurrentTiming;

    /// <summary>Conflicting phases (bit mask)</summary>
    public ushort m_ConflictingPhases;

    /// <summary>Compatible overlap phases (bit mask)</summary>
    public ushort m_CompatibleOverlaps;

    /// <summary>Lane assignments for this phase (entity references)</summary>
    public int2 m_LaneAssignments; // Can store up to 2 lane entity references

    /// <summary>Detector assignments for call placement</summary>
    public ushort m_DetectorMask;

    /// <summary>Whether this phase has active calls</summary>
    public bool m_HasCalls;

    /// <summary>Whether this phase is enabled</summary>
    public bool m_Enabled;

    /// <summary>Whether this phase can be omitted if no calls</summary>
    public bool m_Omittable;

    /// <summary>Whether this phase has pedestrian timing</summary>
    public bool m_HasPedestrian;

    public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
    {
        writer.Write(m_SchemaVersion);
        writer.Write(m_PhaseNumber);
        writer.Write(m_Ring);
        writer.Write((byte)m_Type);
        writer.Write((byte)m_State);
        writer.Write(m_MinimumGreen);
        writer.Write(m_PassageTime);
        writer.Write(m_MaximumGreen);
        writer.Write(m_YellowTime);
        writer.Write(m_AllRedTime);
        writer.Write(m_CurrentTiming);
        writer.Write(m_ConflictingPhases);
        writer.Write(m_CompatibleOverlaps);
        writer.Write(m_LaneAssignments);
        writer.Write(m_DetectorMask);
        writer.Write(m_HasCalls);
        writer.Write(m_Enabled);
        writer.Write(m_Omittable);
        writer.Write(m_HasPedestrian);
    }

    public void Deserialize<TReader>(TReader reader) where TReader : IReader
    {
        Initialization();
        reader.Read(out m_SchemaVersion);
        reader.Read(out m_PhaseNumber);
        reader.Read(out m_Ring);
        reader.Read(out byte type);
        reader.Read(out byte state);
        reader.Read(out m_MinimumGreen);
        reader.Read(out m_PassageTime);
        reader.Read(out m_MaximumGreen);
        reader.Read(out m_YellowTime);
        reader.Read(out m_AllRedTime);
        reader.Read(out m_CurrentTiming);
        reader.Read(out m_ConflictingPhases);
        reader.Read(out m_CompatibleOverlaps);
        reader.Read(out m_LaneAssignments);
        reader.Read(out m_DetectorMask);
        reader.Read(out m_HasCalls);
        reader.Read(out m_Enabled);
        reader.Read(out m_Omittable);
        reader.Read(out m_HasPedestrian);
        m_Type = (PhaseType)type;
        m_State = (PhaseState)state;
    }

    private void Initialization()
    {
        m_SchemaVersion = 1;
        m_PhaseNumber = 1;
        m_Ring = 0;
        m_Type = PhaseType.Vehicular;
        m_State = PhaseState.Rest;
        m_MinimumGreen = 70; // 7 seconds
        m_PassageTime = 30; // 3 seconds
        m_MaximumGreen = 600; // 60 seconds
        m_YellowTime = 40; // 4 seconds
        m_AllRedTime = 20; // 2 seconds
        m_CurrentTiming = 0;
        m_ConflictingPhases = 0;
        m_CompatibleOverlaps = 0;
        m_LaneAssignments = new int2(-1, -1);
        m_DetectorMask = 0;
        m_HasCalls = false;
        m_Enabled = true;
        m_Omittable = true;
        m_HasPedestrian = false;
    }

    public PhaseDefinition()
    {
        Initialization();
    }

    /// <summary>
    /// Checks if this phase conflicts with another phase
    /// </summary>
    public readonly bool ConflictsWith(byte phaseNumber)
    {
        return (m_ConflictingPhases & (1 << phaseNumber)) != 0;
    }

    /// <summary>
    /// Sets conflict relationship with another phase
    /// </summary>
    public void SetConflict(byte phaseNumber, bool conflicts)
    {
        if (conflicts)
        {
            m_ConflictingPhases |= (ushort)(1 << phaseNumber);
        }
        else
        {
            m_ConflictingPhases &= (ushort)~(1 << phaseNumber);
        }
    }

    /// <summary>
    /// Gets the total clearance time (yellow + all red)
    /// </summary>
    public readonly ushort GetClearanceTime()
    {
        return (ushort)(m_YellowTime + m_AllRedTime);
    }

    /// <summary>
    /// Checks if the phase is in a green state
    /// </summary>
    public readonly bool IsGreen()
    {
        return m_State == PhaseState.MinimumGreen || 
               m_State == PhaseState.PassageTime || 
               m_State == PhaseState.MaximumGreen;
    }

    /// <summary>
    /// Checks if the phase is in clearance (yellow or all red)
    /// </summary>
    public readonly bool IsClearing()
    {
        return m_State == PhaseState.Yellow || m_State == PhaseState.AllRed;
    }
}