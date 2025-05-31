# Pix2D Open Source Release TODO

## Security & Sensitive Information (Priority: CRITICAL)
- [x] Audit and remove API keys/credentials from SentryLoggerTarget.cs
- [x] Review and secure license verification logic in PlayMarketLicenseService.cs (remove hardcoded "idkfa" backdoor)
- [x] Move settings storage encryption keys from SettingsService to secure configuration
- [x] Review and secure session storage mechanisms in SessionService.cs
- [x] Audit file permission handling in AndroidFileService for potential security issues

## Documentation & Code Comments (Priority: HIGH)
- [x] Add XML documentation to core interfaces:
  - IProjectService
  - IDrawingService
  - IExportService
  - IPlatformStuffService
- [ ] Document plugin architecture and extension points in Plugins/ directory
- [ ] Document cross-platform considerations in PlatformStuffService implementations
- [ ] Create developer guide for implementing new platform-specific services
- [ ] Add README sections for:
  - Project structure overview
  - Building from source
  - Contributing guidelines
  - Platform-specific setup

## Cross-Platform Support (Priority: HIGH) 
- [x] Refactor platform-specific code in PlatformStuffService to use proper abstraction layers
- [x] Create abstract factory for platform-specific service instantiation
- [x] Standardize file system access across platforms:
  - Unify AndroidFileService and AvaloniaFileService implementations
  - Add consistent error handling for file operations
- [x] Implement proper keyboard input handling for all platforms
- [ ] Add configuration system for platform-specific settings
- [ ] Create platform-specific UI component overrides where needed

## HTML5/Web Integration (Priority: MEDIUM)
- [ ] Clean up and modernize Browser/wwwroot structure:
  - Remove old/unused files (_old directories)
  - Organize static assets
  - Update HTML templates
- [ ] Improve WebGL rendering performance:
  - Add frame buffering optimizations
  - Implement efficient texture handling
- [ ] Create proper web-specific storage service (improve BrowserSettingsService)
- [ ] Add proper web-specific file system handling
- [ ] Implement web-friendly export formats
- [ ] Add proper offline support and caching

## Project Structure & Build System (Priority: MEDIUM)
- [ ] Create proper CI/CD pipeline configuration
- [ ] Add comprehensive test coverage:
  - Unit tests for core services
  - Integration tests for platform implementations
  - UI automation tests
- [ ] Set up proper versioning system
- [ ] Create release automation scripts
- [x] Add proper dependency management:
  - Update NuGet packages
  - Document third-party dependencies
  - Add license information

## Code Quality (Priority: MEDIUM)
- [ ] Implement consistent error handling across the codebase
- [ ] Add proper logging strategy:
  - Remove or properly configure Sentry integration
  - Add configurable log levels
  - Implement proper log rotation
- [ ] Refactor service registration to use proper DI patterns
- [ ] Clean up unused code and remove TODO comments
- [ ] Add code style enforcement (EditorConfig, etc.)

## Feature Preparation (Priority: LOW)
- [ ] Create plugin documentation and examples
- [ ] Add extension points for future features
- [ ] Prepare public API documentation
- [ ] Create migration guides for existing users
- [ ] Add telemetry opt-in/opt-out functionality

## Community & Support (Priority: LOW)
- [ ] Create issue templates
- [ ] Add contributing guidelines
- [ ] Set up community documentation
- [ ] Create sample projects and tutorials
- [ ] Add proper licensing information


# Prelaunch checklist

- [x] License Verification:
  - Confirm the correct LICENSE file is present in the root.
  - Ensure the license is clearly stated in the README.md.
  - Perform a final check for license compatibility issues with all third-party dependencies. If using REUSE, validate compliance.   

- [ ] Security Sweep:
  - Manually search the codebase and commit history one last time for any missed  secrets, API keys, or sensitive information.
  - Consider running basic static analysis security testing (SAST) tools if available.
  - Confirm the SECURITY.md file exists and clearly outlines the reporting process.   

- [ ] Dependency Review:
  - Review the list of all direct and transitive dependencies.
  - Check their licenses again for compatibility.   
  - Assess the maintenance status and known vulnerabilities of dependencies.   
  - Generate/update the Software Bill of Materials (SBOM).   

- [ ] Build & Test Verification:
  - Crucially for pix2d: Perform a clean build of the project from the repository state on all target platforms (Windows, Linux, Android) following the exact steps documented in the README.md or docs/developer_guide.md.   
  - Run all available automated tests (unit, integration) on all target platforms and ensure they pass.

- [ ] Documentation Review:
  - Read through README.md, CONTRIBUTING.md, CODE_OF_CONDUCT.md, SECURITY.md, and the initial content in the docs/ folder from the perspective of a newcomer.
  - Is the information clear, accurate, and sufficient to understand the project, install it, use it basically, and start contributing?. Check for broken links or placeholder text.   

- [ ] Repository Settings:
  - Ensure branch protection rules are enabled and correctly configured for the main branch.   
  - Set up issue templates (.github/ISSUE_TEMPLATE/) to guide bug reports and feature requests.   
  - Confirm repository visibility is set correctly (likely private until this point, then switched to public).

- [ ] Infrastructure & Communication:
  - If planning external communication channels (e.g., Discord server, mailing list mentioned in README.md or CONTRIBUTING.md ), ensure they are set up and monitored.   
  - Ensure CI/CD infrastructure (e.g., GitHub Actions workflows) is configured and operational.   
