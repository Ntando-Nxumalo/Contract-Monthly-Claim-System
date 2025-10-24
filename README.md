# Contract Monthly Claim System (CMCS)

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)  
![License](https://img.shields.io/badge/license-MIT-blue)  
![.NET](https://img.shields.io/badge/.NET-7.0-blueviolet)  
![SQL Server](https://img.shields.io/badge/SQL%20Server-Configured-orange)

## Overview
The Contract Monthly Claim System (CMCS) is a web-based application designed to manage lecturer claims efficiently. It allows lecturers to submit claims with supporting documents and enables coordinators and managers to review, approve, or reject claims. The system includes dashboards, real-time updates, and a chatbot to improve user experience and provide analytics.

## Features
- **Claims Management:** Submit, view, and manage claims with multiple attachments. Automatic total calculations ensure accuracy.
- **Dashboards:** Personalized dashboards for lecturers, coordinators, and managers showing summaries and statuses.
- **Real-Time Updates:** Instant notifications on claim status changes via SignalR.
- **Document Handling:** Secure storage and controlled access for uploaded files.
- **Chatbot Integration:** Assists with queries, claim analytics, and document review.
- **Unit Testing:** Ensures functionality for calculations, validation, and secure document access.

## Technical Details
- **Database:** SQL Server using Code First approach with Entity Framework Core.
- **Models:** Claim, ClaimDocument, ApplicationUser with relationships and validations.
- **Architecture:** MVC design pattern separating controllers, models, and views.
- **Security:** Role-based access control for lecturers, coordinators, and managers.
- **Performance:** Optimized queries and real-time updates for fast and responsive operation.
- **Libraries & Tools:**
  - Entity Framework Core
  - ASP.NET Core Identity
  - SignalR for real-time updates
  - UglyToad.PdfPig for PDF analysis
  - xUnit and Moq for testing

## Setup Instructions
1. Clone the repository:
   ```bash
   git clone <repository-url>
   ```
2. Open the solution in Visual Studio.
3. Update the connection string in `appsettings.json` to match your SQL Server configuration.
4. Run migrations to create the database:
   ```bash
   dotnet ef database update
   ```
5. Build and run the application.
6. Access dashboards and start using the system.

## Usage
- Lecturers can submit claims and attach multiple files.
- Coordinators and Managers can review, approve, or reject claims.
- Chatbot provides assistance and analytics on uploaded claim documents.
- Dashboards display real-time summaries and updates.

## Contribution Guidelines
Contributions are welcome to improve functionality or add features. To contribute:
1. Fork the repository.
2. Create a new branch for your feature:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. Make your changes and commit them with clear messages:
   ```bash
   git commit -m "Add feature: description"
   ```
4. Push your branch to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```
5. Open a pull request detailing the changes and purpose.

Please ensure that all contributions follow best practices for security, performance, and usability.

## Future Enhancements
- OCR support for scanned PDFs.
- Non-PDF document parsing (Word, Excel).
- Advanced AI for complex queries.
- Database indexing for faster analytics.
- Structured logging for monitoring and observability.

## Author
**Ntando Nxumalo**  
