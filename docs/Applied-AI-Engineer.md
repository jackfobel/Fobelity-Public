# Target Role Profile - Applied AI Engineer

## Professional Objective

Pursue an Applied AI Engineer position at Microsoft focused on designing, building, integrating, and operationalizing AI-enabled applications using Microsoft Azure, .NET, agent-based architectures, multimodal AI services, APIs, and cloud-native engineering practices.

This role sits at the intersection of software engineering, cloud architecture, and artificial intelligence. It is less focused on training foundational machine-learning models and more focused on applying models and AI services to solve real business and operational problems through reliable production systems.

[Skip all this, I want to see the demos!](https://github.com/jackfobel/Fobelity-Public/)

## Role Summary

An Applied AI Engineer designs and implements intelligent applications that combine large language models, agents, enterprise data, APIs, automation tools, and user-facing experiences.

The engineer is responsible for turning AI capabilities into usable systems by:

- Integrating language models and Azure AI services into applications.
- Designing agents that reason over data and invoke controlled tools.
- Exposing business and device capabilities through secure APIs.
- Building multimodal experiences using speech, vision, text, and visual interfaces.
- Connecting AI reasoning to real workflows and physical or digital actions.
- Implementing identity, authorization, observability, diagnostics, safety controls, and deployment automation.
- Evaluating system behavior and improving reliability, latency, usability, and cost.

## Target Responsibilities

### AI Application Engineering

- Build AI-enabled applications using Azure AI Foundry and related Azure AI services.
- Integrate models into .NET and ASP.NET Core applications.
- Design prompts, instructions, context management, and tool-selection workflows.
- Develop agent-based systems that can reason, plan, call tools, and return grounded responses.
- Implement structured outputs and application-controlled execution rather than relying solely on unrestricted natural-language responses.

### Agent and Tool Architecture

- Define explicit tools that agents are permitted to invoke.
- Build tool integrations using REST, OpenAPI, MCP, A2A, functions, and service APIs.
- Separate AI reasoning from deterministic business and device operations.
- Implement confirmation, validation, dry-run, authorization, and safety boundaries around actions.
- Coordinate multiple agents or services where responsibilities require specialization.

### Multimodal Experiences

- Integrate speech recognition and text-to-speech into AI interactions.
- Use computer vision and identity context to improve the user experience.
- Combine motion, voice, vision, application state, and device state into a unified interaction flow.
- Provide natural feedback through speech, visual interfaces, avatars, and application telemetry.

### Cloud and Distributed Systems Engineering

- Design edge-to-cloud architectures with clear responsibility boundaries.
- Develop cloud-native services using .NET, ASP.NET Core, Azure Container Apps, Azure Functions, and supporting Azure services.
- Build API facades that safely expose application or device capabilities to AI agents.
- Implement asynchronous processing, background workers, retries, cancellation, health endpoints, and fault handling.
- Create deployment, configuration, secrets, and environment-management strategies.

### Security and Responsible AI

- Protect APIs and services through Microsoft Entra ID and managed identities.
- Apply least-privilege access to agent tools and underlying systems.
- Prevent the AI layer from directly bypassing validation or authorization controls.
- Add auditability, diagnostics, confirmation boundaries, and safe failure behavior.
- Consider privacy, identity data, physical-device access, and human oversight when designing AI workflows.

### Observability and Operations

- Implement structured logging, distributed telemetry, health checks, and diagnostics.
- Monitor model interactions, tool execution, latency, failures, and user-visible outcomes.
- Diagnose issues across edge devices, cloud services, agents, APIs, and external integrations.
- Improve system reliability through fallback behavior, concurrency controls, timeouts, and graceful degradation.
- Evaluate operational cost and resource usage when selecting models and Azure services.

## Evidence from the Mocha/Fobelity Project

The Mocha project demonstrates these Applied AI Engineer capabilities through a functioning Copilot-style assistant for a physical environment.

[Click here to learn more about 'mocha' and watch a few demos.](https://github.com/jackfobel/Fobelity-Public/)

### Multimodal AI

Mocha combines:

- PIR motion detection.
- Raspberry Pi camera capture.
- Azure Face detection and identification.
- Wake-word interaction.
- Azure Speech recognition.
- Azure Speech text-to-speech.
- A visible avatar with viseme-driven mouth movement.
- Device and environmental state.

### Agentic AI

Mocha uses Azure AI Foundry agents as a reasoning and tool-selection layer. The agent interprets user intent and invokes controlled tools rather than directly manipulating devices.

Examples include:

- Retrieving thermostat and HVAC status.
- Controlling a shop mini-split.
- Controlling lighting.
- Routing operations through DeviceHub.
- Calling device-specific services.
- Returning natural-language confirmation to the user.

### Edge-to-Cloud Engineering

The Raspberry Pi provides the local physical interface:

- Motion sensing.
- Camera input.
- Microphone and speaker access.
- Wake-word processing.
- Avatar interaction.
- GPIO experimentation.
- Local health and diagnostic endpoints.

Azure and cloud-hosted services provide:

- Agent reasoning.
- Tool orchestration.
- Device APIs.
- Authentication boundaries.
- Automation.
- Telemetry.
- Service deployment.

### Production-Oriented Engineering

The implementation includes:

- .NET background workers.
- Dependency injection and options-based configuration.
- Microphone concurrency controls.
- Cooldowns that prevent repeated greetings.
- Speech fallback behavior.
- API health and diagnostic endpoints.
- Structured logging.
- Cancellation and timeout handling.
- Dry-run support for device operations.
- Separation between agents, APIs, device services, and edge runtime.

## Existing Professional Foundation

The target role builds on substantial existing software-engineering experience rather than representing a transition into an entry-level engineering position.

Relevant transferable strengths include:

- C# and .NET application development.
- Azure service integration.
- REST APIs and distributed service architecture.
- Enterprise authentication and authorization.
- Complex Dataverse and Dynamics 365 engineering.
- Large-scale data migration and validation.
- Production troubleshooting.
- Logging and telemetry.
- Technical leadership under ambiguity.
- Architecture documentation and reusable engineering patterns.

AI engineering extends this foundation by adding model orchestration, agents, multimodal services, tool integration, AI evaluation, Responsible AI considerations, and AI-specific operational practices.

## Best-Fit Position Types

The strongest role matches would include titles such as:

- Applied AI Engineer
- Senior Applied AI Engineer
- AI Application Engineer
- Azure AI Engineer
- Generative AI Engineer
- AI Agent Engineer
- Software Engineer - AI Applications
- Software Engineer - Copilot and Agents
- Cloud Solution Architect - AI Applications
- Applied AI Solution Engineer

The preferred position is hands-on and engineering-oriented. It should involve building real AI applications and services rather than being limited to AI strategy, sales demonstrations, prompt writing, data-science research, or model training.

## Core Career Narrative

I am an experienced Microsoft software engineer expanding into Applied AI Engineering by applying my .NET, Azure, API, identity, distributed-system, and production-delivery experience to modern AI applications.

Through Mocha, I have designed and implemented a multimodal, agent-driven, edge-to-cloud system that combines Azure AI Foundry, speech, vision, APIs, physical devices, telemetry, and a human-centered avatar experience.

My value is not limited to calling an AI model. I understand how to place AI inside a larger production architecture: defining what the model may do, connecting it to controlled tools, securing the surrounding services, handling failures, observing system behavior, and delivering an experience that produces useful real-world outcomes.
