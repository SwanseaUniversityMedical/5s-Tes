## TRE Agent UI

Frontend web application for managing projects, credentials, and access rules.
Built with **Next.js and TypeScript**.

## Getting Started

#### Create Environment Variables

- Create a `.env` file in the root of the `5s-Tes/Agent/agent-web/` directory.

- Add these env variables within the `.env` file below:

```bash
BETTER_AUTH_SECRET=mU8w2913XafFbRODBy6rZlxSrFvEwesM
BETTER_AUTH_URL=http://localhost:3000
KEYCLOAK_CLIENT_ID=Dare-TRE-UI
KEYCLOAK_CLIENT_SECRET=2de114bc-3599-45f1-9b61-5090c6859dfe
KEYCLOAK_URL=http://localhost:8085
NEXT_PUBLIC_KEYCLOAK_REALM=Dare-TRE
NEXT_PUBLIC_APP_URL=http://localhost:3000
AGENT_API_URL=http://localhost:8072
```

#### Run the development server for TRE Admin UI:

- Install dependencies (Optional)
```bash
npm install
```

- Run development server
```bash
npm run dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the TRE Admin UI.


## Project Structure

This section provides a high-level view of the repository layout and key components.


### Module Breakdown

```
agent-web/
├── api/
├── app/
├── components/
├── lib/
└── types/
```

#### Api Directory

Contains modules for interacting with backend APIs (e.g., projects, credentials, access rules).

```
api/
├── access-rules.ts
├── credentials.ts
└── projects.ts
```

- `api/projects.ts`: Handles API interactions related to project management, such as fetching, creating, or updating projects.

- `api/credentials.ts`: Manages API calls for credential operations, including retrieval, creation, and updates of credentials (Submission & Egress).

- `api/access-rules.ts`: Handles API interactions for access rules (DMN Rules) for fetching, creating, updating, validating and deleting access rules.


#### App Directory

The app directory contains the Next.js App Router structure, including pages, layouts and server-side endpoints.

```
app/
├── access-rules/
├── api/
│   └── auth/
│       └── [...all]/
│           └── route.ts
├── configure-5s-tes/
├── forbidden/
├── logged-out/
├── projects/
│   ├── [projectId]/
│       ├── columns.tsx
│       ├── page.tsx
│       ├── columns.tsx
│   ├── page.tsx
├── sign-in/
├── token-expired/
├── page.tsx
├── layout.tsx
```

- `app/page.tsx`: The page.tsx file defines the main entry point for the application, rendering the homepage/dashboard of the TRE Admin UI.

 It performs an authentication check and renders the UI if the user is "dare-tre-admin", otherwise redirects to the sign-in page.

- `app/layout.tsx`: The layout.tsx file defines the root layout for the application, including common UI elements such as headers, footers, and navigation menus.

- `app/access-rules/`: Contains the page and components related to managing access rules (DMN Rules), including listing, creating, editing, validating, and deleting access rules.

- `app/api/auth/[...all]/route.ts`: This file defines a catch-all API route for handling authentication-related requests, such as sign-in, sign-out, and session management.

- `app/configure-5s-tes/`: Contains the page and components for loading UI and functionality related to configuring 5S-TES.

- `app/forbidden/`: Contains the page displayed when a user tries to access a resource they don't have permission for.

- `app/forbidden/`: Contains the page displayed when a user tries to access a resource they don't have permission for.

- `app/logged-out/`: Contains the page displayed after a user has logged out.

- `app/projects/`: Contains all pages and logic related to project management. This includes the main projects listing page, table column definitions, and dynamic routes for individual projects.

The `[projectId]` subdirectory provides pages and components for viewing and managing specific projects, such as displaying project details and configuring project-specific tables.

- `app/sign-in/`: Contains the sign-in page for user authentication.

- `app/token-expired/`: Contains the page displayed when a user's authentication token has expired and redirects them to sign in again.


#### Components Directory

The components directory contains reusable UI elements and feature-specific building blocks used throughout the application.

Each directory within `components/` corresponds to a specific feature or routes of the application, such as access rules, credentials, projects, and layout.

```components/
├── access-rules/
├── core/
├── credentials/
├── data-table/
├── layout/
├── projects/
└── ui/
    ... (contains generic ShadCN UI components such as buttons, dialogs, tables, etc.)
```

##### access-rules components

- `app/components/access-rules/`: Contains components specific to the access rules (DMN Rules) feature, such as tables, forms, action buttons, and status badges.

     - `access-rules/action-buttons/`: Contains components for action buttons used in the access rules management UI, such as edit, delete, deploy, and refresh buttons.

         - `ActionDeleteButton.tsx`: A button component for deleting access rules.

         - `ActionEditButton.tsx`: A button component for editing access rules.

         - `AddNewRowButton.tsx`: A button component for adding new access rules.

         - `DeployButton.tsx`: A button component for deploying access rules.

         - `RefreshButton.tsx`: A button component for refreshing the access rules list.

     - `access-rules/forms/`: Contains form components for creating and editing access rules.

         - `RulesFormDialog.tsx`: A dialog component that contains a form for creating or editing access rules.

    - `access-rules/status-badge/`: Contains components for displaying the validation status of access rules.

         - `RulesValidationBadge.tsx`: A badge component that indicates the validation status of access rules.

    - `AccessRulesTable.tsx`: A component that renders a Base Table and configures specifically for the access rules with relevant information and actions.

    - `DecisionMetadataCard.tsx`: A component that displays metadata information about a specific access rule decision.

    - `TableActionButtons.tsx`: A component that groups action buttons for each row in the access rules table.

    - `TableRowCodeCell.tsx`: A component that renders a table cell with code formatting for displaying access rule conditions or actions.

    - `TableRulesColumns.tsx`: A component that defines the columns for the access rules table.

    - `TopToolbarButtons.tsx`: A component that contains action buttons displayed in the top toolbar of the access rules page.

    - `ValidationContext.tsx`: A React context that provides validation status and related information for access rules throughout the application.

##### core components

- `app/components/core/`: Contains core components that can be used across different features of the application.

     - `fetch-error.tsx`: A component for displaying error messages related to API fetch operations.

     - `mode-toggle.tsx`: A component for toggling between different modes (e.g., light/dark mode) in the UI.

     - `page-header.tsx`: A component for rendering page headers with consistent styling and structure.


##### credentials Components

- `app/components/credentials/`: Contains components specific to updating the Configure 5s-tes, such as forms, status badges, tabs, and visibility toggles.

     - `CredentialsForm.tsx`: A form component for creating or editing credentials.

     - `CredentialsHelpTooltip.tsx`: A tooltip component that provides help information about credentials.

     - `CredentialsStatusBadge.tsx`: A badge component that indicates the status of credentials (e.g., valid, invalid).

     - `CredentialsTab.tsx`: A component that renders a tab interface for managing different types of credentials (e.g., submission, egress).

     - `CredentialsVisibilityToggle.tsx`: A toggle component for showing or hiding credential values in the UI.

     - `SaveCredentialsButton.tsx`: A button component for saving credential changes.

##### data-table component

- `app/components/data-table/`: Contains components for rendering and managing custom data tables in the application and reused across the codebase when displaying tables.

     - `BaseTable.tsx`: A reusable table component that can be configured with different columns, data, and actions.

     - `BaseTableControls.tsx`: A component that provides controls for the Base Table, such as pagination, filtering, and sorting.

     - `BaseTableFooter.tsx`: A component that renders the footer of the Base Table, which can include summary information or additional actions.

     - `TableColumnSortable.tsx`: A component that renders a sortable column header for the Base Table, allowing users to sort data by that column.

##### layout components

- `app/components/layout/`: Contains components related to the overall layout of the application, such as headers, footers, and navigation menus.

     - `Header.tsx`: A component that renders the header of the application, which may include the logo, navigation links, and user menu.

     - `Footer.tsx`: A component that renders the footer of the application, which may include copyright information and additional links.

     - `components/layout/nav/`: A subdirectory that contains components related to navigation within the application.

         - `MainMenubar.tsx`: Renders the main navigation menu in the navbar, displaying links to Projects, Configure 5S-TES, and Access Rules using styled buttons.

         - `Navbar.tsx`: Composes the main navigation bar by combining the NavbarLogo, MainMenubar, ModeToggle, and UserMenu components, arranging them for responsive layout.

         - `NavbarLogo.tsx`:  Displays the application logo and name ("5S-TES | TRE Admin") as a clickable link to the home page.

         - `UserMenu.tsx`: Provides a user dropdown menu in the navbar, showing the username and options for account management, help desk access, login, and logout actions.

##### projects components

- `app/components/projects/`: Contains components specific to project management features, such as project details, membership forms, and project forms for the `app/projects/` pages.

    - `MembershipForm.tsx`: Provides a form for managing project membership, allowing users to add or remove members from a project.

    - `ProjectDetails.tsx`: Displays detailed information about a specific project, including metadata and configuration.

    - `ProjectForm.tsx`: Offers a form interface for creating or editing project details, supporting project setup and updates.


##### ui Components

- `app/components/ui/`: Contains generic UI components that can be reused across the application, such as buttons, dialogs, tables, and form controls. These components are built on top of a UI library (ShadCN UI) and provide consistent styling and behavior throughout the app.

##### other Components

- `app/components/auth-button.tsx`: Renders a button for authentication actions, such as sign-in or sign-out, integrating with the app’s auth logic.

- `app/components/empty-state.tsx`: Displays a placeholder or informative message when no data is available, improving user experience in empty views.

- `app/components/status-badge.tsx`: Shows a badge indicating status (e.g., success, error, warning) for various entities or actions in the UI.

- `app/components/theme-provider.tsx`: Provides theme context and switching functionality (e.g., light/dark mode) across the application.


#### Lib Directory

The lib directory contains utility functions, API helpers, authentication logic, constants, and custom hooks that encapsulate reusable logic and data manipulation across the application.

```lib/
├── api/
│   ├── helpers.ts
│   └── request.ts
├── constants/
│   ├── index.ts
│   └── radio-options.tsx
├── helpers/
│   └── access-rules-api.ts
├── hooks/
│   ├── use-error-toast.ts
│   └── use-refresh-key.ts
├── auth-client.ts
├── auth-helpers.ts
├── auth.ts
├── helpers.ts
├── utils.ts
```

##### api directory

- `lib/api/`: Contains helper functions for making API requests and handling responses.

     - `helpers.ts`: Provides utility functions for constructing API endpoints, handling authentication tokens, and processing API responses.

     - `request.ts`: Contains a generic function for making API requests, including error handling and response parsing.


##### helpers directory

- `lib/helpers/`: Contains helper functions for specific features or API interactions.

     - `access-rules-api.ts`:  Provides helper functions for formatting and transforming access rules data between API and UI formats.

##### hooks directory

- `lib/hooks/`: Contains custom React hooks for managing state, side effects, and reusable logic.

     - `use-error-toast.ts`: A custom hook for displaying error toast notifications in the UI.

     - `use-refresh-key.ts`: A custom hook that provides a key for triggering refreshes of data or components when it changes.

##### constants directory

- `lib/constants/`: Contains constant values and options used throughout the application.


#### types Directory

- `app/types/`: Contains TypeScript type definitions and interfaces for the application, including API response types, component props, and shared data structures.

---

### Dockerfile

The Dockerfile in the root of the `agent-web/` directory defines the steps to build a Docker image for the TRE Admin UI application. It uses a multi-stage build process to optimize the final image size.

Run the following command in the terminal to build the Docker image:

```bash
docker build -t tre-admin-ui:latest .
```

---


### Learn More

To learn more about Next.js, take a look at the following resources:

- [Next.js Documentation](https://nextjs.org/docs) - learn about Next.js features and API.
- [Learn Next.js](https://nextjs.org/learn) - an interactive Next.js tutorial.

