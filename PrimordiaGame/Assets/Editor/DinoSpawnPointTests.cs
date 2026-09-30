using NUnit.Framework;
using UnityEngine;

public class DinoSpawnPointTests
{
    private GameObject dinoPrefab;
    private DinoProfile profile;

    [SetUp]
    public void SetUp()
    {
        dinoPrefab = new GameObject("Test Dino Prefab");
        dinoPrefab.AddComponent<DinoAI>();
        profile = ScriptableObject.CreateInstance<DinoProfile>();
        dinoPrefab.GetComponent<DinoAI>().profile = profile;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(dinoPrefab);
    }

    [Test]
    public void GetDefaultRespawnRange_UsesLongRangeForAlphaPrefab()
    {
        profile.isAlpha = true;

        DinoSpawnPoint.GetDefaultRespawnRange(dinoPrefab, out float minimum, out float maximum);

        Assert.That(minimum, Is.EqualTo(600f));
        Assert.That(maximum, Is.EqualTo(1200f));
    }

    [Test]
    public void GetDefaultRespawnRange_UsesRegularRangeForRegularPrefab()
    {
        profile.isAlpha = false;

        DinoSpawnPoint.GetDefaultRespawnRange(dinoPrefab, out float minimum, out float maximum);

        Assert.That(minimum, Is.EqualTo(60f));
        Assert.That(maximum, Is.EqualTo(120f));
    }
}
