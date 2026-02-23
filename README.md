# 5S-TES 

![ Five Safes TES logo][5s-tes-logo]

Five Safes TES supports the secure, remote execution of GA4GH TES analyses in Trusted Research Environments (TREs).

- Provides a standardised API for job submission and monitoring
- Enables the execution of GA4GH TES tasks inside TREs
- Supports federated analysis

[![Five Safes TES docs][docs-badge]][5s-tes-docs]

# Submission.Api & Submission.Web 

- Provides an API and user interface for researchers to submit tasks.
- Authenticates and authorises approved researchers.
- Queues validated tasks for the Trusted Research Environment agent to pick up and execute.
- Tracks the status of submitted tasks.

## TRE Agent 

### Agent.Api

#### Core Functionality

- Polls the Submission Layer to retrieve new tasks
- Submits tasks to a GA4GH TES implementation
- Monitors task execution and collects results
- Sends task outputs to the Egress service for approval
- Maintains communication flow between the Submission Layer, TES, and Egress

### Agent.Web

Web FrontEnd for the Agent.Api. Allows TRE Admins to:

- Manage the TREs Projects settings.
- Manage Users allowed to submit to the Project.
- Set DMN rules to configure Ephemeral Credentials creation.

### agent-web

An alternative to the Agent.Web built with Next.js and TypeScript. More information in the directory README.md

## Credentials

### Credentials.Camunda

- Contains core logic handlers for TRE Agent to manage user credentials to access the TRE’s database
- Creates and revokes ephemeral user accounts to access the TRE’s database.
- Uses Camunda, Vault and LDAP services.

### Credentials.Models

- Contains Models and Services shared between Credentials.Camunda and Agent.Api that facilitate creating and revoking
  ephemeral user credentials.

## Shared

### Five Safes TES Core Library

- A shared library that includes Models, Services and Settings shared across the TRE Agent, Submission and Credentials.

[5s-tes-logo]: https://raw.githubusercontent.com/federated-research/docs/refs/heads/main/website/public/logos/five-safes-tes/five_safes_tes_primary.svg

[5s-tes-docs]: https://docs.federated-analytics.ac.uk/five_safes_tes

[docs-badge]: https://img.shields.io/badge/docs-black?style=for-the-badge&labelColor=%23222
