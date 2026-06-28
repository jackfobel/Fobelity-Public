# [Futures] Suggested Repository Documentation

This page lists future documentation that can be added as the repository matures. The goal is to make the project easier for a technical reviewer to understand without overloading the top-level README.

---

## Suggested Component Documentation

Future documentation can be split into deeper component-level files:

- `docs/Component-EdgePiAgent.md`
- `docs/Component-AvatarServer.md`
- `docs/Component-WebGLAvatar.md`
- `docs/Component-DeviceHub.md`
- `docs/Component-MiniSplitControlService.md`
- `docs/Component-Agents-A2A.md`
- `docs/Component-Functions-Schedulers.md`
- `docs/Security-and-Identity.md`
- `docs/Demo-Script.md`
- `docs/Deployment-RaspberryPi.md`
- `docs/Deployment-Azure.md`
- `docs/Telemetry-and-Diagnostics.md`

---

## Recommended Template for Each Component

Each component document should cover:

1. **Purpose**  
   What the component exists to do.

2. **Responsibilities**  
   The boundaries of the component and what it owns.

3. **Runtime flow**  
   How the component behaves during a typical interaction.

4. **APIs and interfaces**  
   Endpoints, message contracts, tool definitions, or integration boundaries.

5. **Configuration**  
   Options, appsettings, environment variables, secrets, and deployment-specific values.

6. **Dependencies**  
   External services, Azure resources, hardware dependencies, and other internal components.

7. **Security model**  
   Authentication, authorization, local endpoint exposure, secrets handling, and least-privilege assumptions.

8. **Deployment notes**  
   How the component runs locally, on Raspberry Pi, or in Azure.

9. **Monitoring and troubleshooting**  
   Logs, health checks, diagnostics, known failure modes, and recovery steps.

10. **Future enhancements**  
    Known gaps, planned improvements, and extension points.

---

## Priority Documentation to Add First

The highest-value next documents are:

1. `docs/Component-EdgePiAgent.md`  
   This is the most technically distinctive piece because it combines .NET on Raspberry Pi, motion, camera, microphone, local speech, avatar integration, and A2A communication.

2. `docs/Component-Agents-A2A.md`  
   This explains how user speech becomes an agent message, how tool use is orchestrated, and how synchronous versus longer-running task responses are handled.

3. `docs/Component-DeviceHub.md`  
   This documents how device capabilities are exposed safely to AI tools.

4. `docs/Demo-Script.md`  
   This can pair with the iPhone demo video and make the system easier to understand for hiring reviewers.

5. `docs/Security-and-Identity.md`  
   This can explain how Azure, service boundaries, and device APIs are intended to be secured.
