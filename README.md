# Mocha — Copilot-Style Azure AI Edge Assistant for Home Automation

**A multimodal Applied AI system built with Azure AI Foundry, .NET on Linux and Windows, Raspberry Pi edge hardware, Azure Speech, Azure Face / Azure AI Vision, WebGL avatar rendering, agent orchestration, and real-world home automation.**

Mocha is an end-to-end AI assistant for a physical environment. It combines Raspberry Pi edge sensing, cloud-hosted AI reasoning, Microsoft/Azure services, device APIs, and a visible avatar interface to create an assistant that can understand who is present, process a spoken request, call tools, control real devices, and communicate back naturally.

The result is a Copilot-style AI assistant for the home and workshop: an assistant that can understand context, reason over available tools, execute real-world actions safely, and respond through a screen-based avatar whose mouth moves with speech using viseme-driven lip synchronization.

## Repository Scope

This public repository is a sanitized portfolio version of a personal Applied AI home automation project. Secrets, deployment-specific configuration, private endpoints, certificates, local scripts, captured media, and environment-specific assets have been intentionally excluded.

## Documentation

- [Applied AI Engineering Highlights](docs/Applied-AI-Engineering-Highlights.md)
- [Current Status and Roadmap](docs/Status-And-Roadmap.md)
- [[Futures] Suggested Repository Documentation](docs/Futures-Repository-Documentation.md)

---

## Example Interaction

At a high level, Mocha turns this interaction:

> “Hey Mocha, turn on the shop lights and set the mini-split to 72 cool.”

into a real-world AI action chain:

> “Sure, Jack. Turning the lights on now. AC is set to 72 for you. Done!”

```text
motion sensor
  → camera capture
  → identity recognition
  → speech-to-text
  → AI agent reasoning
  → tool selection
  → HVAC / lighting / GPIO actuation
  → screen-based avatar response with viseme-driven lip sync
  → telemetry and diagnostic logging
```

*This is not just a chat interface. Mocha connects perception, reasoning, action, and feedback into one working system.*

---

## Project Purpose

This repository represents a hands-on Applied AI engineering project focused on the practical integration work required to make AI useful outside a static chat window.

The system demonstrates:

- End-to-end AI system design from Raspberry Pi edge sensing to Azure-hosted agent orchestration.
- Multimodal interaction using motion, camera input, speech, avatar rendering, device state, and environmental context.
- Agent-based architecture using A2A communication, tool calls, and orchestration patterns.
- Real-world actuation through HVAC, thermostat control, GPIO, device-service integrations, and planned smart lighting groups/scenes.
- Production-oriented engineering practices including service boundaries, configuration, telemetry, health endpoints, diagnostics, fallback paths, and deployment topology.

For an Applied AI Engineer role, this project demonstrates more than prompt usage. It shows the ability to design and integrate a complete AI-enabled system across edge devices, cloud services, APIs, identity, observability, and physical automation.

---

## Current Capability Summary

| Area | Capability | Status |
|---|---|---|
| Edge hardware | Raspberry Pi running .NET services, connected to camera, motion sensor, microphone, speakers, and GPIO/breadboard hardware | Implemented |
| Motion | Passive infrared motion detection used as an activation signal | Implemented |
| Vision | Camera snapshot capture and identity workflow | Implemented |
| Identity | Azure Face detection and known-person identification | Implemented |
| Speech input | Wake-word and command capture through Azure Speech | Implemented |
| Speech output | Speech played through Raspberry Pi speakers | Implemented |
| Avatar | Screen-based avatar response path with visible conversational feedback and viseme-driven lip synchronization | Implemented / evolving |
| Agent communication | A2A client flow from the Pi to an actuator agent | Implemented |
| Agent reasoning | Azure AI Foundry agent/tool architecture for reasoning and device actions | Implemented / evolving |
| HVAC | Workshop mini-split and thermostat-style control paths through device services | Implemented |
| GPIO | Raspberry Pi GPIO output control for local device experiments | Implemented |
| Lighting | Smart lighting groups and scenes | Planned / in progress |
| Operations | Health, status, camera, identity, TTS, voice, and GPIO diagnostic endpoints | Implemented |

---

## High-Level Architecture

```text
[User]
  │
  │  motion / speech / identity / environment context
  ▼
[Raspberry Pi Edge Agent]
  │
  ├─ .NET worker services
  ├─ passive infrared motion sensor
  ├─ camera snapshot capture
  ├─ microphone and speaker pipeline
  ├─ Azure Speech STT / TTS
  ├─ Azure Face detection and identification
  ├─ GPIO / breadboard device control
  └─ local avatar / kiosk interaction
  │
  │  recognized command + context
  ▼
[Azure AI Foundry / Actuator Agent]
  │
  ├─ LLM reasoning
  ├─ user intent evaluation
  ├─ tool selection
  ├─ confirmation / dry-run policy hooks
  └─ A2A task and response handling
  │
  │  tool calls
  ▼
[DeviceHub API]
  │
  ├─ MiniSplitControlService
  ├─ thermostat adapters
  ├─ lighting adapters [planned / evolving]
  ├─ device state APIs
  └─ telemetry and runtime logging
  │
  │  real-world actuation
  ▼
[Mini-Split / Thermostat / Lights / GPIO Devices]
  │
  │  response + state
  ▼
[Avatar.Server on Raspberry Pi]
  │
  ├─ visible avatar on screen
  ├─ local speech through Pi speakers
  └─ viseme-driven lip movement
  │
  ▼
[User Feedback + Logs + Analytics]
```

---

## Runtime Flow

1. **Presence is detected**  
   A passive infrared motion sensor connected to the Raspberry Pi detects movement and updates the local presence state.

2. **Camera context is captured**  
   The Pi camera captures a frame and stores the latest image for identity workflows.

3. **Identity recognition runs**  
   Azure Face detects whether a face is present and identifies a known user when confidence is high enough.

4. **Mocha greets the user**  
   A recognized user can receive a personalized greeting such as, “Welcome back, Jack.”

5. **Wake word or direct command is captured**  
   The microphone pipeline listens for “Hey Mocha” and captures the command that follows.

6. **Speech is converted to text**  
   Azure Speech converts the spoken request into recognized text.

7. **The command is sent to the actuator agent**  
   The Raspberry Pi sends the recognized command through the A2A client path to the actuator agent.

8. **The agent reasons and selects tools**  
   The actuator agent evaluates intent, device state, and safety/confirmation rules before selecting device tools.

9. **DeviceHub executes the action**  
   DeviceHub routes commands to the correct service boundary, such as MiniSplitControlService, thermostat adapters, lighting adapters, or GPIO-facing local APIs.

10. **Mocha responds through the avatar**  
    The response is spoken through the Raspberry Pi speakers and represented by the visible avatar on screen. The avatar is designed for conversational feedback rather than raw console output.

11. **Telemetry is recorded**  
    Edge and cloud services log events, device state, runtime behavior, and diagnostic information for troubleshooting and future analytics.

---

## Core Capabilities

### Identity-Aware Interaction

Mocha can wake when motion is detected, capture a camera frame, detect faces, identify a known user, and personalize the interaction.

Key capabilities:

- Passive infrared motion detection.
- Raspberry Pi camera snapshot capture.
- Azure Face detection and identification.
- Configurable person group and confidence threshold.
- Greeting cooldown to avoid repeated interactions.
- Personalized response path through local speech and avatar output.

### Voice-First Interaction

Mocha supports natural speech interaction through a wake-word-driven command pipeline.

Key capabilities:

- “Hey Mocha” wake-word flow.
- One-shot command mode for single-breath commands.
- Azure Speech recognition and text-to-speech.
- Raspberry Pi microphone selection across configured, ALSA, PulseAudio, and default capture devices.
- Microphone locking to avoid device contention.
- Dry-run and confirmation hooks for safer physical actuation testing.

### Visible Avatar Interface

Mocha is designed to respond through a visible avatar on a screen, not just through terminal logs or raw audio.

Key capabilities:

- Avatar server running on the Raspberry Pi/Linux environment.
- Speech played through the Raspberry Pi speakers.
- HTTP-based route for `/api/tts/say`.
- Optional SignalR hub transport for persistent avatar communication.
- Screen/kiosk interaction model for a workshop display.
- Viseme-driven lip synchronization so the avatar mouth moves with spoken output.

### Agent-Based AI Orchestration

The assistant uses an agent layer to reason over commands and decide which tools should be invoked.

Key capabilities:

- Azure AI Foundry as the AI/agent platform.
- A2A client communication from the Raspberry Pi to the actuator agent.
- Tool-calling architecture for device operations.
- Command packaging from speech-recognized text into agent messages.
- Support for immediate message responses and longer-running A2A task responses.

In practical terms, this means Mocha does not simply answer a question. It can decide which tool to call, invoke that tool, wait for the result, and return a user-safe response.

### Physical Environment Control

Mocha is wired to real device-control paths rather than simulated-only outputs.

Implemented or represented control surfaces include:

- Workshop mini-split HVAC control.
- House thermostat / Ecobee-style thermostat integrations.
- GPIO-controlled devices on the Raspberry Pi.
- DeviceHub-facing APIs for exposing devices to AI tools.
- Smart lighting groups and scenes as a planned/evolving area.

### Telemetry, Diagnostics, and Operations

The project includes practical operational hooks for running the edge service, debugging hardware interactions, and testing AI/device flows safely.

Key capabilities:

- ASP.NET Core minimal API endpoints for health, status, motion, camera, identity, TTS, voice testing, and GPIO.
- Serilog request logging and structured application logging.
- Raspberry Pi systemd deployment notes.
- Speech SDK diagnostic logging.
- Local test endpoints for camera snapshots, identity checks, speech, voice capture, and GPIO output.
- Configuration through options classes, appsettings, and environment variables.

---

## Example Scenario

A user walks into a hot workshop. The Raspberry Pi detects motion from the passive infrared sensor, captures a camera frame, recognizes the user, and Mocha greets them by name through the screen-based avatar.

The user says:

> “Hey Mocha, turn on the shop lights and set the mini-split to 72 cool.”

The system then:

- Converts speech to text.
- Sends the request to the actuator agent.
- Reads current device state through DeviceHub.
- Confirms the requested action is valid.
- Turns on supported device controls.
- Sets the workshop mini-split to Cool at 72°F.
- Returns a spoken confirmation through the Raspberry Pi speakers.
- Animates the visible avatar response with mouth movement aligned to speech.
- Logs the interaction and device state for diagnostics and analytics.

From the user’s perspective, the environment responds naturally to a single spoken request.

---

## Technology Stack

### Microsoft / Azure

- Azure AI Foundry agents.
- Azure Speech for speech recognition and synthesis.
- Azure Face for face detection and identification.
- Entra ID-oriented service boundaries and API security model.
- Azure-hosted services for cloud APIs, agents, automation, and telemetry.

### Edge / Runtime

- Raspberry Pi running Linux.
- .NET / ASP.NET Core on the Pi.
- Background workers for motion, wake-word, speech, and greeting loops.
- Camera, microphone, speakers, motion sensor, and GPIO/breadboard integration.
- Local minimal API endpoints for testing and diagnostics.

### Agent and API Architecture

- A2A communication pattern between edge and agent services.
- DeviceHub-style API façade for AI-safe device access.
- MiniSplitControlService for HVAC/device-specific behavior.
- Tool-oriented boundaries for device state reads and actions.

### Avatar / Interaction

- Avatar.Server running on the Pi.
- Browser/kiosk-style screen interaction.
- HTTP and optional SignalR avatar communication paths.
- Viseme-driven lip sync for visible speech feedback.

### Observability and Operations

- Serilog structured logging.
- Health and status endpoints.
- Speech SDK diagnostics.
- Runtime/device logging for automation, troubleshooting, and future analytics.

---

## Core Components

Mocha is organized as a set of edge, cloud, avatar, agent, and device-service components. The solution is intentionally split across clear service boundaries so the assistant can sense the physical environment locally, reason through Azure-hosted AI services, call tools, actuate real devices, and return visible conversational feedback through an avatar.

| Component                 | Responsibility                                                                                                                                                                                                                                                | Key Technologies                                                                                             |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| `Edge.Pi.Agent`           | Raspberry Pi edge runtime for local sensing and interaction. Handles passive infrared motion sensing, camera capture, wake-word listening, speech recognition, local speech output, GPIO control, identity checks, and communication with the actuator agent. | .NET on Linux, Raspberry Pi, GPIO, camera module, microphone, speakers, Azure Speech, Azure Face, A2A client |
| `Avatar.Server`           | Local avatar and speech coordination service running on the Raspberry Pi. Provides endpoints for speech playback and avatar communication, enabling Mocha to respond through a visible screen-based avatar instead of only raw audio.                         | ASP.NET Core, Azure Speech, HTTP APIs, SignalR, kiosk/browser integration                                    |
| `WebGL / React Avatar UI` | Browser-based avatar experience displayed on the local screen. Renders Mocha visually, supports conversational feedback, and provides the foundation for viseme-driven lip synchronization as speech is produced.                                             | React, WebGL / Three.js-style rendering, browser kiosk UI, viseme mapping                                    |
| `VisemeStreamer`          | Optional real-time channel for broadcasting viseme and speech-animation events to avatar clients. Useful for debugging, alternate avatar clients, and separating speech generation from visual animation.                                                     | SignalR, real-time event streaming                                                                           |
| `A2A AgentServer`         | Agent orchestration layer that receives user requests from the edge runtime and routes them into the AI reasoning flow. Supports immediate responses as well as longer-running agent task patterns where the caller can wait for completion.                  | A2A protocol patterns, Azure AI Foundry agents, .NET services, agent-card style discovery                    |
| `MCP Server / MCP Tools`  | Tooling layer for exposing structured capabilities to AI agents. Represents the “things the assistant can do,” such as querying device state, controlling HVAC, or invoking automation services.                                                              | MCP-style tool contracts, tool calling, .NET APIs                                                            |
| `DeviceHub`               | Unified device-facing API surface for AI tools. Provides an AI-friendly façade over home and workshop devices so agents do not need to know vendor-specific implementation details.                                                                           | ASP.NET Core, REST APIs, authentication/authorization, device adapters                                       |
| `MiniSplitControlService` | Dedicated HVAC service for the workshop mini-split. Encapsulates mini-split commands, state tracking, safety/rules logic, runtime telemetry, and analytics.                                                                                                   | .NET services, HVAC/device API integration, telemetry, rules engine                                          |
| `Automation Adapters`     | Integration layer for device-specific backends such as thermostats, HVAC systems, lighting, GPIO devices, and future smart-home protocols. Keeps vendor-specific logic out of the agent layer.                                                                | Adapter pattern, REST APIs, MQTT/Zigbee-ready design, smart-home integrations                                |
| `AppHost`                 | Local development and orchestration host for running multiple solution services together during development. Helps launch and test the distributed system as a coherent application.                                                                          | .NET Aspire-style hosting pattern, service orchestration                                                     |
| `ServiceDefaults`         | Shared service configuration for cross-cutting concerns such as health checks, logging, telemetry, service discovery, and common runtime behavior.                                                                                                            | .NET service defaults, observability, health endpoints, configuration                                        |

## Repository Structure

```text
README.md
/docs
  Applied-AI-Engineering-Highlights.md
  Status-And-Roadmap.md
  Futures-Repository-Documentation.md

/src
  Edge.Pi.Agent
  Avatar.Server
  DeviceHub
  MiniSplitControlService
  AgentServer
```

The repository is organized around the main runtime boundaries of the system rather than a single monolithic application:

| Area              | Purpose                                                                                                                                                   |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Artificial`      | AI-facing projects, including MCP server/tooling, local MCP experiments, and AI client integration work.                                                  |
| `Automation`      | Home automation platform services, including A2A orchestration, DeviceHub, adapters, edge integration, shared services, and cross-cutting infrastructure. |
| `Avatar`          | User-facing avatar projects, including the avatar server, browser/client UI, and viseme streaming components.                                             |
| `MiniSplit`       | Dedicated workshop HVAC domain, including backend service logic, server APIs, client/test apps, rules, temperature handling, and shared contracts.        |
| `AppHost`         | Development-time host used to compose and run the distributed application locally.                                                                        |
| `ServiceDefaults` | Shared .NET defaults for logging, health checks, telemetry, and common service behavior.                                                                  |

## Component Flow

At runtime, the system follows a perception → reasoning → action → feedback loop:

```text
Motion sensor / camera / microphone
  ↓
Edge.Pi.Agent on Raspberry Pi
  ↓
Azure Speech + Azure Face
  ↓
A2A AgentServer / Azure AI Foundry agent
  ↓
MCP tools / DeviceHub
  ↓
MiniSplitControlService / thermostat / lighting / GPIO devices
  ↓
Avatar.Server + WebGL avatar
  ↓
Spoken response with visible avatar feedback
```

For example, when Jack says:

> “Hey Mocha, turn on the shop lights and set the mini-split to 72 cool.”

Mocha can recognize the spoken command, send it to the agent layer, select the correct device tools, control the physical environment, and respond through the screen-based avatar:

> “Sure, Jack. Turning the lights on now. Done! AC is set to 72 for you.”

The code and documentation should make it clear that Mocha is not a single model call. It is a distributed AI system spanning hardware, cloud agents, device APIs, speech, vision, avatar UI, and operational diagnostics.

