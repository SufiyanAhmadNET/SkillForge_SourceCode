--SkillForge - Learning Management System

SkillForge is a full-stack Learning Management System (LMS) built using ASP.NET Core MVC. The platform allows instructors to create and manage courses, students to enroll and learn, and administrators to manage platform activities - .

* Live :-  https://skillforge-irc1.onrender.com/

* Landing Page
* Student Dashboard
* Instructor Dashboard
* Admin Dashboard
* Course Details Page
* Payment Page

# Tech Stack

 ### Frontend
* Razor Views
* Bootstrap 5
* JavaScript
* jQuery
* AJAX

## Backend
* ASP.NET Core MVC (.NET 8)
* Entity Framework Core
* LINQ
* SQL Server



### Authentication & Security
* Cookie Authentication
* Claims-Based Authorization
* Google OAuth
* Email OTP Verification
* Session Management
* BCrypt Password Hashing

### Third-Party Services
* Razorpay (Payment Gateway)
* Cloudinary (Media Storage)
* MailKit (Email Services)
* QuestPDF (Certificate & Report Generation)

# Features
## Student Module

* Browse and search courses
* View course details and curriculum
* Add courses to wishlist
* Add courses to cart
* Purchase courses using Razorpay
* Access enrolled courses
* Track lesson completion progress
* Download course completion certificates
* View order history and purchased courses

## Instructor Module

* Apply as an instructor
* Create and manage courses
* Add modules and lessons
* Upload course thumbnails and videos
* View enrolled students
* Monitor course performance
* Access earnings and revenue reports
* Generate enrollment and revenue reports

## Admin Module

* Review instructor applications
* Approve or reject instructors
* Review submitted courses
* Approve, reject, or publish courses
* Manage platform categories
* Monitor users and platform activity
* View dashboard analytics and reports

---

# Authentication & Authorization

The application uses role-based access control with separate areas for Students, Instructors, and Admins.

Implemented authentication features:

* Email and password login
* Google OAuth login
* Email OTP verification
* Cookie-based authentication
* Claims-based authorization
* Session management
* Password hashing using BCrypt

Each role can access only the features assigned to them.

---

# Project Structure

```text
Areas/
 ├─ Admin/
 ├─ Instructor/
 └─ User/

Controllers/
Services/
Interfaces/
Models/
ViewModels/
Data/
wwwroot/
```

### Folder Overview
* Areas → Role-specific modules
* Controllers → Request handling
* Services → Business logic
* Interfaces → Service contracts
* Models → Database entities
* ViewModels → UI data transfer models
* Data → DbContext and database configuration
* wwwroot → Static files (CSS, JS, Images)

---

# Architecture
The project follows an N-Tier Architecture.

```text
Razor Views
      ↓
Controllers
      ↓
Services
      ↓
Entity Framework Core
      ↓
SQL Server
```

Business logic is separated into service classes and injected using Dependency Injection to keep the code organized and maintainable.

---

# Database Overview
Main entities used in the project:

* Users
* Instructor Profiles
* Categories
* Courses
* Modules
* Lessons
* Enrollments
* Orders
* Payments
* Certificates
* User Progress

These entities work together to manage course publishing, enrollments, payments, and learning progress.

---

# Third-Party Integrations

### Razorpay

Used for course payments and order processing.

### Cloudinary

Used for storing and managing course images and videos.

### MailKit

Used for sending OTP verification and system emails.

### Google OAuth

Used for Google account login.

### QuestPDF

Used for certificate and report generation.

---

# Setup & Installation

### Clone Repository

```bash
git clone https://github.com/yourusername/SkillForge.git
```

### Configure Database

Update the SQL Server connection string in:

```json
appsettings.json
```

### Configure External Services

Add  credentials for:
* Razorpay
* Cloudinary
* Google OAuth
* MailKit SMTP
---

# Future Improvements
* Quiz and assessment module
* Course reviews and ratings
* Real-time notifications using SignalR
* Discussion forum for students and instructors
* Advanced analytics dashboard
---

# Author
Sufiyan Ahmad

* LinkedIn: - https://www.linkedin.com/in/sufiyan26/
* GitHub: - https://github.com/SufiyanAhmadNET/SkillForge
* Email :- sufiyanahmad1590@gmail.com
