using System;
using Unity.Entities;
using Unity.Collections;
using C2VM.TrafficLightsEnhancement.Components;

namespace C2VM.TrafficLightsEnhancement.Tests;

/// <summary>
/// Simple test to validate ring & barrier logic without full game dependencies
/// </summary>
public static class RingBarrierTests
{
    public static void TestBasicPhaseProgression()
    {
        // Test basic phase progression logic
        var controller = new RingBarrierController();
        
        // Test phase activation
        controller.SetPhaseActive(1, true);
        if (!controller.IsPhaseActive(1))
        {
            throw new Exception("Phase 1 should be active");
        }
        
        // Test phase deactivation
        controller.SetPhaseActive(1, false);
        if (controller.IsPhaseActive(1))
        {
            throw new Exception("Phase 1 should not be active");
        }
        
        Console.WriteLine("Basic phase progression test passed");
    }
    
    public static void TestPhaseDefinition()
    {
        var phase = new PhaseDefinition();
        phase.m_PhaseNumber = 1;
        phase.m_Ring = 0;
        phase.m_State = PhaseDefinition.PhaseState.MinimumGreen;
        
        if (!phase.IsGreen())
        {
            throw new Exception("Phase should be in green state");
        }
        
        phase.m_State = PhaseDefinition.PhaseState.Yellow;
        if (!phase.IsClearing())
        {
            throw new Exception("Phase should be in clearing state");
        }
        
        Console.WriteLine("Phase definition test passed");
    }
    
    public static void TestCallData()
    {
        var call = new CallData(1, CallData.CallType.Vehicular, 1, 100);
        
        if (call.m_Phase != 1)
        {
            throw new Exception("Call should be for phase 1");
        }
        
        if (!call.IsActive())
        {
            throw new Exception("Call should be active");
        }
        
        call.Clear(200);
        if (call.IsActive())
        {
            throw new Exception("Call should not be active after clearing");
        }
        
        Console.WriteLine("Call data test passed");
    }
    
    public static void TestDetectorData()
    {
        var detector = new DetectorData();
        detector.m_AssignedPhase = 1;
        detector.m_Type = DetectorData.DetectorType.Presence;
        detector.m_State = DetectorData.DetectorState.Clear;
        
        // Test vehicle detection
        detector.UpdateState(true, 100);
        if (detector.m_State != DetectorData.DetectorState.Occupied)
        {
            throw new Exception("Detector should be occupied");
        }
        
        if (!detector.ShouldPlaceCall())
        {
            throw new Exception("Detector should place call when occupied");
        }
        
        Console.WriteLine("Detector data test passed");
    }
    
    public static void RunAllTests()
    {
        try
        {
            TestBasicPhaseProgression();
            TestPhaseDefinition();
            TestCallData();
            TestDetectorData();
            Console.WriteLine("All ring & barrier tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
            throw;
        }
    }
}