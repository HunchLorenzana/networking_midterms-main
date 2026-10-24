using System.Runtime.CompilerServices;

// Lets the EditMode tests use test-only hooks such as ProfileService.UseSaveFolderForTests
[assembly: InternalsVisibleTo("Kitbash.Tests.EditMode")]
