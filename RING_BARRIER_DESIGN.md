# Ring & Barrier Traffic Signal Controller

This implementation adds real-world traffic signal controller functionality to Cities: Skylines 2, based on professional traffic control systems used by agencies worldwide.

## Overview

The ring & barrier system implements a dual-ring, barrier-synchronized traffic controller that mimics the behavior of commercial controllers like those made by Econolite, McCain, or Siemens.

### Key Features

- **Dual Ring Operation**: Two independent rings that can run different phases simultaneously
- **Barrier Synchronization**: Phases must complete before advancing past barrier points  
- **Vehicle Detection**: Lane-based detectors place calls for service when vehicles are present
- **Real-World Timing**: Industry standard timing states and transitions
- **Actuated Control**: Responsive to actual traffic demand rather than fixed timing

## Components

### RingBarrierController
Main controller component that manages:
- Ring state and active phases
- Barrier configuration and synchronization
- Cycle timing for coordinated operation
- Controller mode (actuated, coordinated, manual, flash)

### PhaseDefinition
Defines individual traffic phases with:
- Real-world timing parameters (min green, passage time, max green, yellow, all-red)
- Phase states (rest, calls present, minimum green, passage time, yellow, all-red)
- Conflict matrix with other phases
- Ring assignment and phase type

### DetectorData
Vehicle detection zones that:
- Monitor specific lanes for vehicle presence
- Place calls for service on assigned phases
- Provide extension time during green phases
- Support different detector types (presence, pulse, speed, queue)

### CallData
Call for service management:
- Tracks calls placed by detectors
- Manages call priorities and timeouts
- Supports different call types (vehicular, pedestrian, emergency, transit)

### TimingParameters
Coordinated timing configuration:
- Cycle length and offset timing
- Split allocations and force-off points
- Coordination mode and priority settings

## Phase Operation

### Standard 8-Phase Configuration

**Ring 1:**
- Phase 1: Main road through movements (east-west)
- Phase 2: Main road left turns
- Phase 3: Cross road through movements (north-south)  
- Phase 4: Cross road left turns

**Ring 2:**
- Phases 5-8: Pedestrian movements and overlaps

### Timing States

1. **Rest**: Phase inactive, no calls present
2. **Calls Present**: Calls exist but phase not yet green
3. **Minimum Green**: Initial green time to ensure safe starts
4. **Passage Time**: Gap extension based on vehicle detection
5. **Maximum Green**: Force termination after max time
6. **Yellow**: Clearance interval for stopping vehicles
7. **All Red**: Additional clearance for intersection safety

### Barrier Logic

Barriers ensure coordinated operation between rings:
- Both rings must complete their current phases before advancing
- Prevents conflicting movements from running simultaneously
- Maintains proper phase sequence and timing relationships

## Vehicle Detection

### Detection Process

1. **Zone Monitoring**: Each detector monitors a specific zone on its assigned lane
2. **Call Placement**: When vehicles are detected, calls are placed for the assigned phase
3. **Extension Logic**: Active detectors can extend green time during passage time
4. **Call Management**: Calls are tracked until served or timed out

### Detector Types

- **Presence**: Continuous detection while vehicle is in zone
- **Pulse**: Momentary detection when vehicle passes through zone
- **Speed**: Measures vehicle speed for advanced applications
- **Queue**: Detects queued vehicles for overflow management

## Integration

The ring & barrier system integrates seamlessly with the existing Cities: Skylines 2 traffic light infrastructure:

- **Pattern Selection**: Available as "RingBarrier" pattern in the UI
- **Lane Assignment**: Automatically assigns lanes to appropriate phases
- **Conflict Management**: Prevents conflicting movements from running together
- **UI Integration**: Works with existing mod UI and configuration system

## Configuration

### Basic Setup

1. Select "Ring & Barrier" pattern from traffic light menu
2. System automatically configures standard 4-phase operation
3. Detectors are placed on approaching lanes
4. Timing parameters use sensible defaults

### Advanced Configuration

- **Phase Timing**: Adjust minimum/maximum green times per phase
- **Detector Placement**: Configure detection zones and sensitivity
- **Coordination**: Set up arterial progression with cycle timing
- **Conflicts**: Customize which phases can run simultaneously

## Real-World Equivalents

This implementation follows established traffic engineering practices:

- **NEMA Standards**: Compatible with standard 8-phase NEMA configurations
- **Professional Controllers**: Similar operation to Econolite ASC/3, McCain M50/M60, Siemens M60
- **Traffic Engineering**: Implements MUTCD timing standards and practices
- **Actuated Operation**: Responsive control based on ITE guidelines

## Future Enhancements

Planned improvements include:

- **Preemption**: Emergency vehicle and railroad preemption
- **Transit Priority**: Bus and rail priority operation  
- **Coordination**: Multi-intersection arterial progression
- **Adaptive Control**: ML-based traffic-responsive timing
- **Performance Monitoring**: Detailed operational statistics