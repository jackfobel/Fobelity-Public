# Applied AI Engineering Highlights

This project is intended to show practical engineering across the full Applied AI stack: multimodal input, Azure AI services, agent orchestration, tool execution, edge hardware, API design, and real-world feedback.

The result is a Copilot-style AI assistant for a physical environment. Mocha can understand who is present, reason about what the user asks, call tools safely, control real devices, and communicate back naturally through a visible avatar.

---

## Multimodal System Design

Mocha combines multiple input and output modes into a single interaction loop:

| Mode | Role in the system |
|---|---|
| Motion | Activation signal that lets the system know someone may be present |
| Vision | Identity context using camera snapshots and Azure Face identification |
| Speech | Primary command interface through wake-word and speech-to-text flows |
| LLM reasoning | Decision layer that interprets user intent and selects actions |
| Device tools | Action layer for HVAC, thermostat, GPIO, and future lighting flows |
| Avatar output | Feedback layer through a visible avatar, local speech, and viseme-driven mouth movement |

This creates a complete perception → reasoning → action → feedback loop rather than a static question-answer interface.

---

## Agentic Tool Use

The agent does not simply answer a question. It selects and invokes tools that change real device state.

Examples:

- Query current thermostat or mini-split state.
- Determine whether a requested command is valid.
- Route HVAC commands through DeviceHub.
- Execute device-specific behavior through MiniSplitControlService.
- Use dry-run or confirmation hooks for safer testing.
- Return a user-safe confirmation response.

The important engineering pattern is that AI is used as a reasoning layer over explicit tools and controlled APIs. The tools define what the assistant is allowed to do, while the agent decides when and how to use them.

---

## Edge-to-Cloud Architecture

The project separates local real-time concerns from cloud-based reasoning and device orchestration.

### Edge responsibilities

The Raspberry Pi handles the local physical interface:

- Motion sensor monitoring.
- Camera capture.
- Microphone input.
- Local speaker output.
- Avatar/kiosk interaction.
- Wake-word and command capture.
- Local diagnostic endpoints.
- GPIO and breadboard-connected device experiments.

### Cloud and service responsibilities

Cloud and service layers handle reasoning, APIs, and automation boundaries:

- Azure AI Foundry agents for reasoning and tool selection.
- DeviceHub-style API façade for AI-safe access to devices.
- MiniSplitControlService and thermostat adapters for vendor/device-specific behavior.
- Authentication, service boundaries, and telemetry flows.
- Automation and scheduling patterns for future expansion.

This separation is important because hardware interaction, speech capture, and avatar rendering have different latency and reliability concerns than LLM reasoning and cloud APIs.

---

## Microsoft and Azure Alignment

Mocha is intentionally built around the Microsoft and Azure ecosystem:

- Azure AI Foundry for agent reasoning.
- Azure Speech for speech recognition and text-to-speech.
- Azure Face for face detection and identity recognition.
- .NET / ASP.NET Core for the Raspberry Pi edge agent and local APIs.
- Entra ID-oriented thinking for protected service-to-service APIs.
- Azure-hosted services for DeviceHub, agents, automation, and telemetry.

For an Applied AI Engineer role, the relevant signal is not just that the project uses AI services. The relevant signal is that it composes AI services, edge runtime, APIs, identity, telemetry, and hardware into a working system.

---

## Production-Oriented Design

The implementation includes practical engineering decisions that matter outside of a demo:

- Background workers for motion and wake-word loops.
- Cooldowns to avoid repeated greetings.
- Microphone locking to avoid device contention.
- Microphone device fallback across configured device, ALSA, PulseAudio, and SDK defaults.
- Safe speech fallback from avatar HTTP endpoint to local synthesis.
- Local health and test endpoints.
- Configuration through options classes and environment variables.
- Structured logs for troubleshooting.
- Dry-run support for safer actuator testing.
- Separation of edge, agent, DeviceHub, and device-service responsibilities.

These design choices make the project more credible as a real engineering system instead of a one-off prototype.

---

## Demo Narrative

A short video demo can show the system moving through the full perception → reasoning → action loop.

Useful proof points to capture:

1. Walk into the workshop.
2. Show motion detection activating the system.
3. Capture a camera frame.
4. Show Mocha recognizing the user by name.
5. Say: “Hey Mocha, turn on the shop lights and set the mini-split to 72 cool.”
6. Show the command being recognized as text or logged.
7. Show the actuator agent selecting tools or returning an action result.
8. Show the HVAC/device state changing.
9. Show Mocha responding through the visible avatar.
10. Show logs or a status endpoint confirming updated device state.

The demo should communicate that Mocha is not a static chat UI. It is a multimodal AI system that senses context, reasons, calls tools, acts in the real world, and responds through a human-centered interface.

---

## Why This Project Matters

Mocha demonstrates the same architectural themes showing up in modern AI platforms and Copilot-style products:

- AI as a reasoning layer over tools and data.
- Agents coordinating tasks across services.
- Multimodal interaction with voice, vision, UI, and device state.
- Secure APIs exposing real capabilities to AI systems.
- Human-centered feedback through natural language and avatar interfaces.
- Reliable engineering around latency, hardware, diagnostics, and safety.

For an Applied AI Engineer role, this project demonstrates the ability to design and integrate a complete AI-enabled system across edge devices, cloud services, APIs, identity, observability, and physical automation.
