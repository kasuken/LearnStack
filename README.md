# LearnStack - Content Management System

## Overview
LearnStack is a learning content management application built with Blazor Server 10 and MudBlazor. It helps you organize and track URLs of blog posts, podcasts, videos, and other AI-related learning resources, while also planning content creation ideas.

## Features

### Learning Resource Management
- Add and manage learning resources with URLs, titles, and descriptions
- Support for multiple content types:
  - Blog Posts
  - Podcasts
  - Videos
  - Articles
  - Courses
  - Documentation
- Track status: To Learn, In Progress, Completed
- Set priority levels: High, Medium, Low
- Add tags for better organization
- Take notes and capture key learnings
- Search and filter by content type, status, priority, and tags

### Content Ideas Planning
- Plan and track content creation ideas
- Link ideas to source learning resources
- Track idea status: Idea, In Progress, Published
- Set priorities for your content creation queue
- Add outlines and notes for each idea

## Tech Stack
- **Framework**: Blazor Server 10
- **Language**: .NET 10 with C#
- **UI Library**: MudBlazor 8.15.0
- **Database**: SQL Server with Entity Framework Core 10.0.3
- **Authentication**: ASP.NET Core Identity

## Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server (LocalDB or full SQL Server)

### Setup

1. **Clone the repository**
   ```powershell
   git clone <repository-url>
   cd LearnStack
   ```

2. **Update the connection string**
   Edit `appsettings.json` and update the connection string if needed:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=LearnStackDb;Trusted_Connection=True;MultipleActiveResultSets=true"
   }
   ```

3. **Apply database migrations**
   ```powershell
   cd LearnStack
   dotnet ef database update
   ```

4. **Run the application**
   ```powershell
   dotnet run
   ```

5. **Navigate to the application**
   Open your browser and go to `https://localhost:5001`

### First Time Setup
1. Register a new account using the Register link
2. Confirm your email (in development, check the console output for the confirmation link)
3. Start adding learning resources from the Resources page
4. Plan content ideas from the Content Ideas page

## Project Structure

```
LearnStack/
├── Components/
│   ├── Pages/
│   │   ├── Home.razor              # Welcome page
│   │   ├── Resources.razor         # Learning resources management
│   │   └── ContentIdeas.razor      # Content ideas planning
│   ├── Shared/
│   │   ├── ResourceCard.razor      # Learning resource card component
│   │   ├── ResourceForm.razor      # Add/edit resource form
│   │   ├── ContentIdeaCard.razor   # Content idea card component
│   │   └── ContentIdeaForm.razor   # Add/edit idea form
│   └── Layout/
│       └── MainLayout.razor        # Main layout with navigation
├── Data/
│   ├── Models/
│   │   ├── LearningResource.cs     # Learning resource model
│   │   ├── ContentIdea.cs          # Content idea model
│   │   ├── ContentType.cs          # Content type enum
│   │   ├── ContentStatus.cs        # Status enum
│   │   └── Priority.cs             # Priority enum
│   ├── ApplicationDbContext.cs     # EF Core DbContext
│   └── Migrations/                 # Database migrations
├── Services/
│   ├── ILearningResourceService.cs # Learning resource service interface
│   ├── LearningResourceService.cs  # Learning resource service implementation
│   ├── IContentIdeaService.cs      # Content idea service interface
│   └── ContentIdeaService.cs       # Content idea service implementation
└── Program.cs                       # Application startup
```

## Usage

### Managing Learning Resources
1. Navigate to **Learning Resources** from the sidebar
2. Click **Add New** to add a resource
3. Fill in the URL, title, description, and other details
4. Use filters to view resources by status or content type
5. Click on a resource card to edit or delete it
6. Use the menu on each card to change status quickly

### Planning Content Ideas
1. Navigate to **Content Ideas** from the sidebar
2. Click **Add New Idea** to create a content idea
3. Fill in the title, description, outline, and other details
4. Filter ideas by status
5. Edit or delete ideas as needed

## Database Schema

### LearningResource
- Id (PK)
- Url
- Title
- Description
- ContentType (enum)
- Status (enum)
- Priority (enum)
- Notes
- DateAdded
- DateCompleted
- Tags
- CustomOrder
- UserId (FK)

### ContentIdea
- Id (PK)
- Title
- ContentType (enum)
- Description
- Outline
- Status (enum)
- Priority (enum)
- Notes
- DateCreated
- DatePublished
- UserId (FK)

### ContentIdeaResource (Join Table)
- ContentIdeaId (FK)
- LearningResourceId (FK)

## Future Enhancements
- URL metadata fetching (Open Graph tags)
- Drag-and-drop reordering
- Browser extension for quick URL capture
- Export/import functionality
- Statistics and progress tracking
- Collaborative features
- Mobile app

## License
[Your License Here]

## Contributing
Contributions are welcome! Please feel free to submit a Pull Request.

