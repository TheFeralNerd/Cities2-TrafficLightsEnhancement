using Colossal.Serialization.Entities;
using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Components;

/// <summary>
/// Main component for ring & barrier based traffic signal control
/// </summary>
public struct RingBarrierController : IComponentData, ISerializable
{
    public enum ControllerMode : byte
    {
        /// <summary>Free running actuated control</summary>
        Actuated = 0,
        /// <summary>Coordinated operation with offset timing</summary>
        Coordinated = 1,
        /// <summary>Manual control mode</summary>
        Manual = 2,
        /// <summary>Flash mode - all lights flashing red/yellow</summary>
        Flash = 3
    }

    public enum ControllerState : byte
    {
        /// <summary>Normal operation</summary>
        Normal = 0,
        /// <summary>Emergency vehicle preemption active</summary>
        Preemption = 1,
        /// <summary>Railroad preemption active</summary>
        Railroad = 2,
        /// <summary>Maintenance mode</summary>
        Maintenance = 3
    }

    private ushort m_SchemaVersion;

    /// <summary>Current controller operating mode</summary>
    public ControllerMode m_Mode;

    /// <summary>Current controller state</summary>
    public ControllerState m_State;

    /// <summary>Number of rings (typically 2 for standard intersections)</summary>
    public byte m_RingCount;

    /// <summary>Current active phases per ring (bit mask)</summary>
    public ushort m_ActivePhases;

    /// <summary>Barrier configuration - defines which phases must complete before advancing</summary>
    public ushort m_BarrierConfig;

    /// <summary>Cycle length in deciseconds for coordinated operation</summary>
    public ushort m_CycleLength;

    /// <summary>Offset from master clock in deciseconds</summary>
    public ushort m_Offset;

    /// <summary>Current cycle timer in deciseconds</summary>
    public ushort m_CycleTimer;

    /// <summary>Force off table - defines when phases must end in coordinated mode</summary>
    public uint m_ForceOffTable;

    /// <summary>Pedestrian clearance time in deciseconds</summary>
    public byte m_PedestrianClearance;

    /// <summary>All red clearance time in deciseconds</summary>
    public byte m_AllRedClearance;

    public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
    {
        writer.Write(m_SchemaVersion);
        writer.Write((byte)m_Mode);
        writer.Write((byte)m_State);
        writer.Write(m_RingCount);
        writer.Write(m_ActivePhases);
        writer.Write(m_BarrierConfig);
        writer.Write(m_CycleLength);
        writer.Write(m_Offset);
        writer.Write(m_CycleTimer);
        writer.Write(m_ForceOffTable);
        writer.Write(m_PedestrianClearance);
        writer.Write(m_AllRedClearance);
    }

    public void Deserialize<TReader>(TReader reader) where TReader : IReader
    {
        Initialization();
        reader.Read(out m_SchemaVersion);
        reader.Read(out byte mode);
        reader.Read(out byte state);
        reader.Read(out m_RingCount);
        reader.Read(out m_ActivePhases);
        reader.Read(out m_BarrierConfig);
        reader.Read(out m_CycleLength);
        reader.Read(out m_Offset);
        reader.Read(out m_CycleTimer);
        reader.Read(out m_ForceOffTable);
        reader.Read(out m_PedestrianClearance);
        reader.Read(out m_AllRedClearance);
        m_Mode = (ControllerMode)mode;
        m_State = (ControllerState)state;
    }

    private void Initialization()
    {
        m_SchemaVersion = 1;
        m_Mode = ControllerMode.Actuated;
        m_State = ControllerState.Normal;
        m_RingCount = 2;
        m_ActivePhases = 0;
        m_BarrierConfig = 0b1100_0011; // Standard 4-phase barrier: phases 1,2 | 3,4
        m_CycleLength = 1200; // 120 seconds default
        m_Offset = 0;
        m_CycleTimer = 0;
        m_ForceOffTable = 0;
        m_PedestrianClearance = 70; // 7 seconds
        m_AllRedClearance = 30; // 3 seconds
    }

    public RingBarrierController()
    {
        Initialization();
    }

    /// <summary>
    /// Checks if a phase is currently active in any ring
    /// </summary>
    public readonly bool IsPhaseActive(byte phase)
    {
        return (m_ActivePhases & (1 << phase)) != 0;
    }

    /// <summary>
    /// Sets a phase as active
    /// </summary>
    public void SetPhaseActive(byte phase, bool active)
    {
        if (active)
        {
            m_ActivePhases |= (ushort)(1 << phase);
        }
        else
        {
            m_ActivePhases &= (ushort)~(1 << phase);
        }
    }

    /// <summary>
    /// Checks if barrier requirements are met for ring advancement
    /// </summary>
    public readonly bool CanAdvancePastBarrier(byte currentRing)
    {
        // Implementation depends on specific barrier configuration
        // For now, simple check if all required phases in barrier are complete
        return true; // Placeholder
    }
}