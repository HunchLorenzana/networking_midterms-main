using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Runs against a temporary folder, never the real save
public class ProfileServiceTests
{
    private const string FILE_NAME = "kitbash_profile.json";

    private string folder;
    private string mainPath;

    [SetUp]
    public void SetUp()
    {
        folder = Path.Combine(Path.GetTempPath(), "KitbashProfileTests_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        mainPath = Path.Combine(folder, FILE_NAME);
        ProfileService.UseSaveFolderForTests(folder);
    }

    [TearDown]
    public void TearDown()
    {
        ProfileService.UseSaveFolderForTests(null);
        Directory.Delete(folder, true);
    }

    [Test]
    public void SavedProfile_LoadsBack()
    {
        ProfileService.AddScrap(123);
        ProfileService.UseSaveFolderForTests(folder);

        Assert.AreEqual(123, ProfileService.Current.Scrap);
    }

    [Test]
    public void SecondSave_KeepsThePreviousOneAsBackup()
    {
        ProfileService.AddScrap(100);
        ProfileService.AddScrap(50);

        Assert.IsTrue(File.Exists(mainPath + ".bak"));
        Assert.IsFalse(File.Exists(mainPath + ".tmp"));
        StringAssert.Contains("100", File.ReadAllText(mainPath + ".bak"));
    }

    [Test]
    public void CorruptMainFile_RecoversFromBackup()
    {
        ProfileService.AddScrap(100);
        ProfileService.AddScrap(50);
        File.WriteAllText(mainPath, "{ this is not json");
        ProfileService.UseSaveFolderForTests(folder);

        Assert.AreEqual(100, ProfileService.Current.Scrap);
        Assert.IsTrue(File.Exists(mainPath + ".corrupt"));
        StringAssert.Contains("100", File.ReadAllText(mainPath));
    }

    [Test]
    public void MissingMainFile_RecoversFromAFinishedTempFile()
    {
        ProfileService.AddScrap(70);
        File.Move(mainPath, mainPath + ".tmp");
        ProfileService.UseSaveFolderForTests(folder);

        Assert.AreEqual(70, ProfileService.Current.Scrap);
    }

    [Test]
    public void NothingReadable_StartsANewProfile_AndKeepsTheBadFile()
    {
        File.WriteAllText(mainPath, "garbage");
        LogAssert.Expect(LogType.Error, new Regex("could not be read"));
        ProfileService.UseSaveFolderForTests(folder);

        Assert.AreEqual(0, ProfileService.Current.Scrap);
        Assert.IsTrue(File.Exists(mainPath + ".corrupt"));
    }
}
