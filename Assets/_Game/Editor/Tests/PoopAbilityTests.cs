using NUnit.Framework;
using UnityEngine;

public sealed class PoopAbilityTests
{
    [Test]
    public void CooldownBlocksImmediateSecondUseAndKeepsConfiguredLifetime()
    {
        GameObject abilityObject = new GameObject("PoopAbilityTest");
        GameObject poopTemplate = CreatePoopTemplate();
        try
        {
            PoopAbility ability = abilityObject.AddComponent<PoopAbility>();
            ability.Configure(null, poopTemplate, 2f, 10f, 100, 0.2f);

            Assert.That(ability.TryUse(), Is.True);
            Assert.That(ability.TryUse(), Is.False);
            Assert.That(ability.RemainingCooldown, Is.GreaterThan(0f));
            Assert.That(ability.LifetimeSeconds, Is.EqualTo(10f));
            Assert.That(ability.MaxActivePoops, Is.EqualTo(100));
            Assert.That(ability.RingIntervalSeconds, Is.EqualTo(0.2f));
        }
        finally
        {
            Object.DestroyImmediate(abilityObject);
            Object.DestroyImmediate(poopTemplate);
        }
    }

    [Test]
    public void UseBeyondCapacityEvictsOldestAndKeepsConfiguredCount()
    {
        GameObject abilityObject = new GameObject("PoopAbilityCapacityTest");
        GameObject poopTemplate = CreatePoopTemplate();
        try
        {
            PoopAbility ability = abilityObject.AddComponent<PoopAbility>();
            ability.Configure(null, poopTemplate, 0f, 10f, 3, 0.2f);
            int successfulUses = 0;
            ability.Used += () => successfulUses++;

            for (int index = 0; index < 4; index++)
                Assert.That(ability.TryUse(), Is.True, $"第 {index + 1} 次应成功生成");

            Assert.That(successfulUses, Is.EqualTo(4));
            Assert.That(ability.ActivePoopCount, Is.EqualTo(3));
            Assert.That(ability.IsAtCapacity, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(abilityObject);
            Object.DestroyImmediate(poopTemplate);
        }
    }

    [Test]
    public void RingIndexMovesOutwardAtConfiguredWidth()
    {
        Assert.That(PoopAbility.CalculateRingIndex(0f, 1.9f), Is.EqualTo(0));
        Assert.That(PoopAbility.CalculateRingIndex(1.89f, 1.9f), Is.EqualTo(0));
        Assert.That(PoopAbility.CalculateRingIndex(1.9f, 1.9f), Is.EqualTo(1));
        Assert.That(PoopAbility.CalculateRingIndex(3.8f, 1.9f), Is.EqualTo(2));
    }

    [Test]
    public void SheepPoopReactionStartsWithoutMovingPhysicsRoot()
    {
        GameObject sheep = new GameObject("PoopReactionSheep");
        try
        {
            sheep.AddComponent<SpriteRenderer>();
            SheepVisualAnimator animator = sheep.AddComponent<SheepVisualAnimator>();
            Vector3 originalPosition = sheep.transform.position;

            animator.PlayPoopReaction();

            Assert.That(animator.IsPoopReactionPlaying, Is.True);
            Assert.That(sheep.transform.position, Is.EqualTo(originalPosition));
        }
        finally
        {
            Object.DestroyImmediate(sheep);
        }
    }

    [Test]
    public void PoopRendersOneOrderBehindItsSheep()
    {
        GameObject sheep = new GameObject("PoopSortingSheep");
        GameObject poop = new GameObject("PoopSortingVisual");
        try
        {
            SpriteRenderer sheepRenderer = sheep.AddComponent<SpriteRenderer>();
            sheepRenderer.sortingLayerName = "Default";
            sheepRenderer.sortingOrder = 7;

            SpriteRenderer poopRenderer = poop.AddComponent<SpriteRenderer>();
            PoopVisual visual = poop.AddComponent<PoopVisual>();
            visual.Configure(10f, sheepRenderer);

            Assert.That(poopRenderer.sortingLayerID, Is.EqualTo(sheepRenderer.sortingLayerID));
            Assert.That(poopRenderer.sortingOrder, Is.EqualTo(6));
        }
        finally
        {
            Object.DestroyImmediate(sheep);
            Object.DestroyImmediate(poop);
        }
    }

    private static GameObject CreatePoopTemplate()
    {
        GameObject template = new GameObject("PoopTemplate");
        template.AddComponent<SpriteRenderer>();
        template.AddComponent<PoopVisual>();
        return template;
    }
}
