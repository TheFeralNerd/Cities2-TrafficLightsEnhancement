using Colossal.Serialization.Entities;
using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Components;

/// <summary>
/// Timing parameters for coordinated signal operation
/// </summary>
public struct TimingParameters : IComponentData, ISerializable
{
    public enum CoordinationMode : byte
    {
        /// <summary>Free running operation</summary>
        Free = 0,
        /// <summary>Coordinated with master clock</summary>
        Coordinated = 1,
        /// <summary>Manual coordination</summary>
        Manual = 2
    }

    private ushort m_SchemaVersion;

    /// <summary>Coordination mode</summary>
    public CoordinationMode m_Mode;

    /// <summary>Cycle length in deciseconds</summary>
    public ushort m_CycleLength;

    /// <summary>Natural cycle length (free running) in deciseconds</summary>
    public ushort m_NaturalCycle;

    /// <summary>Offset from master reference point in deciseconds</summary>
    public ushort m_Offset;

    /// <summary>Split allocations for each phase (percentage * 10)</summary>
    public ulong m_SplitTable; // 8 phases x 8 bits each

    /// <summary>Force off table - when phases must end in cycle</summary>
    public ulong m_ForceOffTable; // 8 phases x 8 bits each

    /// <summary>Yield points for coordination</summary>
    public ushort m_YieldPoints;

    /// <summary>Permissive periods where phases can be skipped</summary>
    public ushort m_PermissivePeriods;

    /// <summary>Coordination priority factor (0-255)</summary>
    public byte m_CoordinationPriority;

    /// <summary>Maximum time to hold for coordination in deciseconds</summary>
    public ushort m_MaxHoldTime;

    /// <summary>Minimum time to extend for coordination in deciseconds</summary>
    public ushort m_MinExtendTime;

    /// <summary>Whether coordination is enabled</summary>
    public bool m_CoordinationEnabled;

    /// <summary>Whether to use force off points</summary>
    public bool m_UseForceOff;

    /// <summary>Whether to allow early return to coordination</summary>
    public bool m_AllowEarlyReturn;

    public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
    {
        writer.Write(m_SchemaVersion);
        writer.Write((byte)m_Mode);
        writer.Write(m_CycleLength);
        writer.Write(m_NaturalCycle);
        writer.Write(m_Offset);
        writer.Write(m_SplitTable);
        writer.Write(m_ForceOffTable);
        writer.Write(m_YieldPoints);
        writer.Write(m_PermissivePeriods);
        writer.Write(m_CoordinationPriority);
        writer.Write(m_MaxHoldTime);
        writer.Write(m_MinExtendTime);
        writer.Write(m_CoordinationEnabled);
        writer.Write(m_UseForceOff);
        writer.Write(m_AllowEarlyReturn);
    }

    public void Deserialize<TReader>(TReader reader) where TReader : IReader
    {
        Initialization();
        reader.Read(out m_SchemaVersion);
        reader.Read(out byte mode);
        reader.Read(out m_CycleLength);
        reader.Read(out m_NaturalCycle);
        reader.Read(out m_Offset);
        reader.Read(out m_SplitTable);
        reader.Read(out m_ForceOffTable);
        reader.Read(out m_YieldPoints);
        reader.Read(out m_PermissivePeriods);
        reader.Read(out m_CoordinationPriority);
        reader.Read(out m_MaxHoldTime);
        reader.Read(out m_MinExtendTime);
        reader.Read(out m_CoordinationEnabled);
        reader.Read(out m_UseForceOff);
        reader.Read(out m_AllowEarlyReturn);
        m_Mode = (CoordinationMode)mode;
    }

    private void Initialization()
    {
        m_SchemaVersion = 1;
        m_Mode = CoordinationMode.Free;
        m_CycleLength = 1200; // 120 seconds
        m_NaturalCycle = 1000; // 100 seconds
        m_Offset = 0;
        m_SplitTable = 0; // Will be calculated based on phase timing
        m_ForceOffTable = 0;
        m_YieldPoints = 0;
        m_PermissivePeriods = 0;
        m_CoordinationPriority = 128; // Medium priority
        m_MaxHoldTime = 100; // 10 seconds
        m_MinExtendTime = 50; // 5 seconds
        m_CoordinationEnabled = false;
        m_UseForceOff = false;
        m_AllowEarlyReturn = true;
    }

    public TimingParameters()
    {
        Initialization();
    }

    /// <summary>
    /// Gets split time for a specific phase in deciseconds
    /// </summary>
    public readonly ushort GetPhaseSplit(byte phase)
    {
        if (phase >= 8) return 0;
        
        byte splitPercent = (byte)((m_SplitTable >> (phase * 8)) & 0xFF);
        return (ushort)(m_CycleLength * splitPercent / 1000);
    }

    /// <summary>
    /// Sets split time for a specific phase
    /// </summary>
    public void SetPhaseSplit(byte phase, ushort splitTime)
    {
        if (phase >= 8) return;
        
        byte splitPercent = (byte)((splitTime * 1000) / m_CycleLength);
        ulong mask = ~(0xFFUL << (phase * 8));
        m_SplitTable = (m_SplitTable & mask) | ((ulong)splitPercent << (phase * 8));
    }

    /// <summary>
    /// Gets force off time for a specific phase in deciseconds from cycle start
    /// </summary>
    public readonly ushort GetPhaseForceOff(byte phase)
    {
        if (phase >= 8) return 0;
        
        byte forceOffPercent = (byte)((m_ForceOffTable >> (phase * 8)) & 0xFF);
        return (ushort)(m_CycleLength * forceOffPercent / 1000);
    }

    /// <summary>
    /// Sets force off time for a specific phase
    /// </summary>
    public void SetPhaseForceOff(byte phase, ushort forceOffTime)
    {
        if (phase >= 8) return;
        
        byte forceOffPercent = (byte)((forceOffTime * 1000) / m_CycleLength);
        ulong mask = ~(0xFFUL << (phase * 8));
        m_ForceOffTable = (m_ForceOffTable & mask) | ((ulong)forceOffPercent << (phase * 8));
    }

    /// <summary>
    /// Calculates current position in cycle based on timer
    /// </summary>
    public readonly float GetCyclePosition(ushort cycleTimer)
    {
        return (float)cycleTimer / m_CycleLength;
    }

    /// <summary>
    /// Checks if we're in a yield point for coordination
    /// </summary>
    public readonly bool IsInYieldPoint(ushort cycleTimer)
    {
        ushort position = (ushort)((cycleTimer * 16) / m_CycleLength);
        return (m_YieldPoints & (1 << position)) != 0;
    }

    /// <summary>
    /// Calculates splits automatically based on phase timing
    /// </summary>
    public void CalculateAutoSplits(PhaseDefinition[] phases)
    {
        if (phases == null || phases.Length == 0) return;

        ushort totalMinTime = 0;
        foreach (var phase in phases)
        {
            if (phase.m_Enabled)
            {
                totalMinTime += (ushort)(phase.m_MinimumGreen + phase.GetClearanceTime());
            }
        }

        // Distribute remaining time proportionally
        ushort remainingTime = (ushort)(m_CycleLength - totalMinTime);
        for (byte i = 0; i < phases.Length && i < 8; i++)
        {
            if (phases[i].m_Enabled)
            {
                ushort minTime = (ushort)(phases[i].m_MinimumGreen + phases[i].GetClearanceTime());
                ushort additionalTime = (ushort)(remainingTime / phases.Length);
                SetPhaseSplit(i, (ushort)(minTime + additionalTime));
            }
        }
    }
}