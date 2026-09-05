using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ScatteredSheepTests
{
    [Test]
    public void RecruitedScatteredSheepRestoresItsColorBeforeScatter()
    {
        GameObject flockObject = new GameObject("TestFlock");
        GameObject sheepObject = new GameObject("TestSheep");

        try
        {
            FlockController flock = flockObject.AddComponent<FlockController>();
            SpriteRenderer renderer = sheepObject.AddComponent<SpriteRenderer>();
            Color originalColor = new Color(0.35f, 0.42f, 0.9f, 0.8f);
            renderer.color = originalColor;
            SheepMember member = sheepObject.AddComponent<SheepMember>();

            MethodInfo addMember = typeof(FlockController).GetMethod(
                "AddMember",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(addMember);
            Assert.IsTrue((bool)addMember.Invoke(flock, new object[] { member }));

            ScatteredSheep.Scatter(member, Vector2.right);
            RecruitableSheep recruitable = sheepObject.GetComponent<RecruitableSheep>();

            Assert.IsNotNull(recruitable);
            Assert.AreNotEqual(originalColor, renderer.color);
            Assert.IsTrue(flock.TryRecruit(recruitable));
            Assert.AreEqual(originalColor, renderer.color);
        }
        finally
        {
            Object.DestroyImmediate(sheepObject);
            Object.DestroyImmediate(flockObject);
        }
    }
}
