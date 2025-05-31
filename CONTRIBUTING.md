# Contributing to Pix2D

Thank you for your interest in contributing to Pix2D! This document provides guidelines and information about contributing to this project.

## Ways to Contribute

- Reporting bugs
- Suggesting new features
- Improving documentation
- Submitting code changes
- Writing tutorials or blog posts
- Helping other users

## Development Setup

### Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK or later
- For Android development: Android Studio and Android SDK
- For Linux development: Mono development tools
- Git

### Getting Started

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR-USERNAME/pix2d.git
   ```
3. Add the upstream repository:
   ```bash
   git remote add upstream https://github.com/ORIGINAL-OWNER/pix2d.git
   ```
4. Create a new branch for your work:
   ```bash
   git checkout -b feature/your-feature-name
   ```

### Building the Project

#### Windows
1. Open `Sources/Pix2d.sln` in Visual Studio
2. Build the solution
3. Run the Desktop project

#### Linux
1. Install required dependencies
2. Use `dotnet build` from the Sources directory
3. Run using `dotnet run`

#### Android
1. Open the Android project in Android Studio
2. Sync Gradle files
3. Build and run

## Coding Style

- Follow the existing code style in the project
- Use meaningful variable and function names
- Comment your code when the logic isn't immediately clear
- Keep methods focused and concise
- Write unit tests for new functionality
- Use async/await for asynchronous operations
- Follow SOLID principles
- Keep UI and business logic separated

## Making Changes

1. Create a new branch for your changes
2. Write clear, concise commit messages
3. Include tests when adding new features
4. Update documentation as needed
5. Follow the existing code style
6. Keep changes focused and atomic

## Submitting Changes

1. Push your changes to your fork
2. Create a Pull Request (PR)
3. Fill in the PR template with:
   - Description of changes
   - Related issue numbers
   - Screenshots (if applicable)
   - Testing done
4. Wait for review

### Pull Request Process

1. Ensure your code builds without errors
2. Update documentation if needed
3. Add tests for new functionality
4. Wait for CI checks to pass
5. Address review feedback
6. Maintain a clean commit history

## Bug Reports

When filing a bug report, please include:

- Clear, descriptive title
- Steps to reproduce
- Expected behavior
- Actual behavior
- Screenshots if applicable
- System information:
  - OS version
  - Pix2D version
  - Device information (for mobile)
  - Any relevant logs

## Feature Requests

When suggesting new features:

- Check existing issues first
- Provide clear use cases
- Explain the benefit to users
- Consider implementation complexity
- Discuss potential drawbacks

## Questions?

Feel free to ask questions by:
- Opening a GitHub Discussion
- Joining our community chat
- Checking the documentation

## License

By contributing to Pix2D, you agree that your contributions will be licensed under the MIT License.