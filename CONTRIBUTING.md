# Contributing to TempTrimmer

Thank you for considering a contribution. The project is small and focused, so please keep pull requests equally focused.

## Getting started

1. Fork the repository and clone your fork.
2. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
3. Build and run the tests:

   ```bash
   dotnet build
   dotnet test
   ```

4. Open the solution (`TempTrimmer.slnx`) in Visual Studio 2026 or VS Code with the C# Dev Kit.

## Before submitting a pull request

- All existing tests must pass (`dotnet test`).
- New logic should be accompanied by tests in `tests/TempTrimmer.Tests/`.
- Keep commits focused. Prefer one logical change per PR.
- Follow the existing code style (see `.editorconfig`). In particular: no unnecessary comments, British English in prose, no trailing whitespace.
- Update `README.md` if you are changing behaviour or adding configuration options.

## Reporting issues

Please use the [GitHub issue tracker](https://github.com/alasdaircs/TempTrimmer/issues). Include the extension version, the App Service OS/tier, and the steps to reproduce.

## Licence

By contributing you agree that your contributions will be licenced under the [MIT Licence](LICENSE.md).
