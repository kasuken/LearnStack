# LearnStack - Copilot Instructions

## Project Overview
LearnStack is a learning content management application for organizing and tracking URLs of blog posts, podcasts, videos, and other AI-related learning resources. The app helps users manage their learning journey with prioritization, note-taking, and content creation planning.

## Tech Stack
- **Frontend Framework**: Blazor Server 10
- **Language**: .NET 10 with C#
- **Styling**: MudBlazor
- **Data**: SQLServer with Entity Framework Core

## Core Features

### 1. Learning Resource Management
- Add learning resources with URL, title, and description
- Support multiple content types: blog posts, podcasts, videos, articles, courses, documentation
- Each resource should have:
  - URL (required)
  - Title (auto-populate from URL metadata when possible, or manual entry)
  - Description (optional)
  - Category/Type (blog, podcast, video, etc.)
  - Priority level (high, medium, low, or numeric ordering)
  - Status (to learn, in progress, completed)
  - Notes field (for findings and note-taking)
  - Date added
  - Date completed (when marked as done)
  - Tags for additional categorization

### 2. Organization and Filtering
- **Search**: Full-text search across title, description, and notes
- **Filter by category**: Show only specific content types (blogs, podcasts, videos, etc.)
- **Filter by status**: To Learn, In Progress, Completed
- **Filter by priority**: High to low
- **Search by tags**: Multiple tag support

### 3. Todo List System
- Mark resources as "To Learn", "In Progress", or "Completed"
- Completed items should be visually separated but remain accessible
- Show counts for each status category
- Quick toggle between views
- Completed items should include completion date

### 4. Prioritization and Ordering
- **Drag and drop** functionality to reorder items within the learning queue
- Visual indicators for priority levels
- Ability to manually set priority (numeric or high/medium/low)
- Sort options: by priority, date added, alphabetically, custom order

### 5. Note-Taking
- Rich notes field for each resource
- Support for:
  - Key learnings
  - Important quotes or snippets
  - Follow-up questions
  - Related resources
- Notes should be searchable
- Consider markdown support for formatting

### 6. Content Creation Ideas Section
- Separate section for content creation planning
- Each idea linked to source learning resources
- Fields for content ideas:
  - Title/Topic
  - Content type (blog post, video, tweet thread, tutorial, etc.)
  - Description/Outline
  - Source resources (link to learned items)
  - Status (idea, in progress, published)
  - Priority
  - Notes
- Ability to mark which learned resources inspired each content idea

## UI/UX Guidelines

### Layout
- **Clean, modern interface** with good use of white space
- **Mobile-responsive** design
- **Sidebar or tab navigation** for main sections:
  - Learning Queue (active items)
  - Completed Resources
  - Content Ideas
  - All Resources
- **Header** with search bar and quick filters

### Component Structure
- Separate components for:
  - ResourceCard (individual learning resource display)
  - ResourceForm (add/edit resources)
  - ResourceList (list view with drag-and-drop)
  - FilterBar (category, status, priority filters)
  - SearchBar
  - ContentIdeaCard
  - ContentIdeaForm
  - DragDropContainer

### Color Coding
- Use TailwindCSS colors to differentiate:
  - Categories (different badge colors)
  - Status (to learn: blue, in progress: yellow, completed: green)
  - Priority (high: red, medium: yellow, low: gray)

### Interactions
- Smooth animations for drag-and-drop
- Hover states for better interactivity
- Modal or slide-over for adding/editing items
- Inline editing where appropriate
- Confirmation dialogs for destructive actions

## Data Persistence
- Use **localStorage** for initial implementation
- Structure data to be easily migrated to backend later
- Export/import functionality for backup
- Consider JSON format for data portability

## Development Practices
- Type-safe with TypeScript throughout
- Use custom hooks for data management (useLocalStorage, useResources, etc.)
- Component composition for reusability
- Follow React 19 best practices
- Utilize TailwindCSS utilities, avoid custom CSS where possible
- Implement proper error handling
- Add loading states for async operations (URL metadata fetching)

## Feature Enhancements (Future Considerations)
- URL metadata fetching (Open Graph tags for auto-populating title/description)
- Browser extension for quick URL capture
- Progress tracking (e.g., percentage watched/read)
- Time estimates for each resource
- Learning streaks and statistics
- Social sharing of learning paths
- Collaborative features
- Backend integration (API, database)
- Authentication and user accounts

## Testing Approach
- Component testing for key UI elements
- Hook testing for data management logic
- Integration tests for user flows
- Manual testing for drag-and-drop interactions

## Accessibility
- Semantic HTML elements
- ARIA labels where needed
- Keyboard navigation support
- Focus management in modals
- Screen reader friendly

## Performance Considerations
- Virtualized lists for large datasets
- Debounced search
- Optimistic UI updates
- Memoization where appropriate
- Code splitting for different sections

## Tone of voice
Talk to me like you are Batman.