# PROMPT: Create a Professional PowerPoint Presentation for the RentLog-SqlServer Project

Act as a professional software engineer, systems analyst, and PowerPoint presentation designer. Create a polished, professional PowerPoint presentation documenting my **RentLog-SqlServer** project, including its purpose, system requirements, existing functionality, recent development work, bug fixes, new features, technical challenges, and future improvements.

The presentation must be suitable for a software engineering project demonstration, academic assessment, or stakeholder presentation.

## 1. Project Background

**Project name:** RentLog-SqlServer

**Database:** Microsoft SQL Server — this must remain the database used by the project.

**Main purpose:** A rental property maintenance management system that allows maintenance issues to be reported and tracked, enables landlords to manage maintenance requests, and supports communication and actions related to resolving reported problems.

The system focuses on improving communication, accountability, maintenance tracking, and visibility of issue resolution.

## 2. Cover the Following System Functionality

Explain the existing and intended functionality of the system, distinguishing between features that are already implemented and features that still require verification.

Include:

- Maintenance issue reporting and issue logs.
- Maintenance reports and the tracking of issue statuses.
- Landlord actions for managing reported maintenance issues.
- Contractor quotations, including attaching or submitting a quote and recording the quote amount.
- The ability for landlords to mark maintenance issues as fixed.
- Landlord comments appearing in the maintenance issue report log.
- User registration.
- Password recovery.
- Database persistence using Microsoft SQL Server.

Explain how these features contribute to a complete maintenance management workflow.

**Important:** Do not claim that a feature works correctly unless this has been confirmed. Identify any feature that has been requested but still requires implementation, testing, or verification.

## 3. Recent Development Work and Requirements

Explain the development priorities for the project:

1. Keep Microsoft SQL Server as the database.
2. Prioritise maintenance reports, landlord actions, and maintenance status tracking.
3. Investigate and fix the contractor quotation attachment and submission process.
4. Ensure that quote amounts are captured and saved correctly.
5. Fix the “Mark Fixed” action so that it updates the issue status correctly.
6. Ensure that landlord comments are saved and displayed in the maintenance issue report log.
7. Add user registration.
8. Add password recovery.
9. Preserve existing functionality and avoid introducing regressions.

Explain why these requirements must be tested together to ensure that the maintenance workflow remains functional after changes.

## 4. Technical Problem: Duplicate Registration Route

Include a dedicated slide explaining the registration page error encountered during testing.

The application displayed an ASP.NET Core routing exception:

“AmbiguousMatchException: The request matched multiple endpoints.”

The error identified two routes:

- `/register` in `RentLog.Components.Pages.Register`
- `/register` in `RentLog.Components.Pages.Registration`

Explain that both components appear to be registered to the same route, causing the application to be unable to determine which page should handle a request to `/register`.

Describe the likely solution:

- Inspect `Register.razor` and `Registration.razor`.
- Check the `@page` directives in both files.
- Ensure that only one component owns the `/register` route.
- Keep `/register` assigned to the intended registration form.
- If the second component is needed as a separate page, give it a distinct route, such as `/registration`.
- If the second component is only a reusable component, remove its page-routing directive.
- Search the rest of the application for any additional conflicting route registrations.
- Restart the application and verify that registration loads correctly.

Clearly explain that this is a routing conflict and does not, by itself, prove that the registration database logic is broken.

## 5. Other Technical Challenges

Include a slide about the application startup issue encountered during development:

`Failed to bind to address http://127.0.0.1:5116: address already in use.`

Explain that this usually means another process is already using the port. Recommend stopping the previous application process or running the application on a different available port.

Also mention the HTTPS redirection warning:

`Failed to determine the https port for redirect.`

Explain that this should be checked separately from the duplicate registration route because it is a different configuration issue.

## 6. System Architecture and Technology

Create an architecture slide illustrating the typical flow of the application:

User → Web interface → Application logic → Entity Framework Core (if confirmed in the project) → Microsoft SQL Server.

Include the relevant components only where they can be verified. Clearly label any assumed architecture rather than presenting assumptions as confirmed facts.

Explain the responsibilities of each layer, including user interaction, business logic, data access, and persistent storage.

Do not invent frameworks, libraries, database tables, or architectural components that have not been confirmed.

## 7. Testing and Quality Assurance

Include a testing plan with a table containing:

| Test area | Expected result |
|---|---|
| Application startup | Application starts without port conflicts |
| Registration page | `/register` loads one unambiguous registration page |
| Registration submission | Valid details are processed and saved correctly |
| Password recovery | Recovery workflow behaves as designed |
| Contractor quote attachment | Supported quote files can be attached successfully |
| Quote submission | Quote details and amount are processed and persisted |
| Mark Fixed | Maintenance issue status updates correctly |
| Landlord comments | Comments appear in the appropriate issue report log |
| Maintenance reports | Correct issue details and statuses are displayed |
| SQL Server persistence | Data remains available after restarting the application |
| Regression testing | Existing maintenance functionality remains operational |

Clearly distinguish between tests planned for execution and tests that have actually passed.

## 8. Security and Reliability

Include relevant security considerations:

- Secure password storage using an appropriate password-hashing mechanism.
- Safe password recovery using time-limited, single-use reset tokens.
- Server-side input validation.
- Appropriate authorization for landlord actions.
- File validation and upload restrictions for contractor quotations.
- Protection of sensitive configuration values and database credentials.
- Safe handling of errors without exposing detailed stack traces to ordinary users.
- Appropriate audit logging for important maintenance actions.

Present these as implementation requirements or verification points unless their implementation has been confirmed.

## 9. Recommended Slide Structure

Create approximately 12–15 slides:

1. Title slide — RentLog-SqlServer.
2. Project overview and problem statement.
3. Project objectives and scope.
4. System users and responsibilities.
5. Existing maintenance management workflow.
6. Key functionality and feature requirements.
7. Contractor quotations and landlord maintenance actions.
8. User registration and password recovery.
9. System architecture and SQL Server integration.
10. Technical challenges encountered.
11. Duplicate `/register` route: root cause and proposed fix.
12. Testing and quality assurance plan.
13. Security, reliability, and data integrity.
14. Current status, outstanding work, and next steps.
15. Conclusion and expected benefits.

Adjust the slide count if necessary to maintain clarity and avoid overcrowding.

## 10. Presentation Design Requirements

Use a modern, professional software-engineering presentation style.

- Use a consistent colour palette with dark navy, teal, white, and light grey.
- Use clear headings and concise bullet points.
- Include diagrams, workflow illustrations, architecture diagrams, and a testing table.
- Use readable fonts and consistent spacing.
- Keep each slide focused on one main idea.
- Include short speaker notes explaining what the presenter should say.
- Avoid excessive text, unnecessary animations, and decorative graphics.
- Include code or error-message snippets only when they help explain a technical problem.
- Use accurate terminology and explain technical concepts in accessible language.
- Ensure that diagrams and tables are readable when projected.

## 11. Accuracy Requirements

Do not invent project progress, successful test results, screenshots, database schemas, implementation details, or completed fixes.

Use the project files and any supporting documentation I provide as the primary evidence. If I upload the project source code, inspect it before making claims about implementation.

Where the status of a feature is unknown, label it “Requires verification” or “Planned.” Where a problem has been identified but no fix has been confirmed, label the solution “Proposed fix.”

Do not imply that the duplicate registration route, quote submission, Mark Fixed action, or landlord comment logging has been successfully fixed unless the evidence confirms this.

## 12. Final Deliverable

Generate an actual editable **PowerPoint (.pptx) presentation**, not merely an outline or a list of suggested slides.

Include:
- A professional title slide.
- A logical narrative from project background to technical issues and proposed solutions.
- Clear architecture and workflow diagrams.
- A testing and implementation roadmap.
- Speaker notes for each slide, where supported.
- A concise conclusion explaining the value of the RentLog-SqlServer system and the remaining steps needed to ensure reliability.

The final presentation should demonstrate an understanding of the system, its requirements, the technical problems encountered, and the approach needed to deliver a reliable maintenance management application.
