using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SheepNotificationTests
{
    private GameObject root;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("NotificationTests", typeof(RectTransform));
        Time.timeScale = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        Object.DestroyImmediate(root);
    }

    [Test]
    public void BurstKeepsOnlyLatestThreeNamesAndReusesThreePanels()
    {
        JoinToastView view = CreateJoinView();
        for (int index = 0; index < 40; index++)
        {
            view.Show("羊" + index);
            Assert.That(view.GetComponentsInChildren<Image>().Length, Is.LessThanOrEqualTo(3));
        }

        Advance(view, 0.13f);
        Advance(view, 0.13f);
        CollectionAssert.AreEquivalent(new[]
        {
            "“羊37”加入了族群！", "“羊38”加入了族群！", "“羊39”加入了族群！"
        }, view.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray());
        Assert.That(view.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(3));
        Advance(view, 2f);
        Assert.That(view.GetComponentsInChildren<Image>().Length, Is.Zero);
        view.Show("新伙伴");
        Assert.That(view.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(3));
    }

    [Test]
    public void ContinuousFastRecruitmentCannotStarveReplacementAnimations()
    {
        JoinToastView view = CreateJoinView();
        int updatedLabels = 0;
        int visibleFrames = 0;
        for (int index = 0; index < 60; index++)
        {
            view.Show("羊" + index);
            Advance(view, 0.02f);
            if (index > 10 && view.GetComponentsInChildren<Image>().Any(image =>
                image.GetComponent<CanvasGroup>().alpha > 0f))
                visibleFrames++;
            if (view.GetComponentsInChildren<TMP_Text>().Any(text =>
                text.text.Contains("“羊" + Mathf.Max(3, index - 8) + "”")))
                updatedLabels++;
        }
        Assert.That(updatedLabels, Is.GreaterThan(0), "Labels must keep updating during a sustained 50 sheep/second burst.");
        Assert.That(visibleFrames, Is.GreaterThan(0), "A fast stream must not leave every slot permanently transparent.");
        Advance(view, 0.13f);
        CollectionAssert.AreEquivalent(new[]
        {
            "“羊57”加入了族群！", "“羊58”加入了族群！", "“羊59”加入了族群！"
        }, view.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray());
    }

    [Test]
    public void FourthNoticeSqueezesOldestBeforeShowingReplacement()
    {
        JoinToastView view = CreateJoinView();
        view.Show("一");
        view.Show("二");
        view.Show("三");
        Advance(view, 0.2f);
        TMP_Text oldest = view.GetComponentsInChildren<TMP_Text>().Single(text => text.text.Contains("“一”"));
        view.Show("四");
        Advance(view, 0.06f);
        Assert.That(oldest.transform.parent.localScale.y, Is.LessThan(1f));
        Assert.That(oldest.GetComponentInParent<CanvasGroup>().alpha, Is.LessThan(1f));
        Advance(view, 0.07f);
        Assert.That(oldest.text, Is.EqualTo("“四”加入了族群！"));
        Assert.That(view.GetComponentsInChildren<Image>().Length, Is.EqualTo(3));
    }

    [Test]
    public void DiscoveryQueueDeduplicatesTypesAndDoesNotInterruptRareMessage()
    {
        SheepDiscoveryToastView view = CreateDiscoveryView();
        view.Show("gold", Entry("gold", "金羊", SheepQuality.Gold), 1);
        Advance(view, 0.2f);
        SheepDetailCardView card = view.GetComponent<SheepDetailCardView>();
        view.Show("blue", Entry("blue", "蓝羊", SheepQuality.Blue), 1);
        view.Show("blue", Entry("blue", "蓝羊", SheepQuality.Blue), 1);
        Advance(view, 3f);
        Assert.That(card.SheepNameText.text, Is.EqualTo("金羊"));
        Advance(view, 2f);
        Advance(view, 0.2f);
        Assert.That(card.SheepNameText.text, Is.EqualTo("蓝羊"));
        Assert.That(card.RarityText.color, Is.EqualTo(SheepCardView.GetQualityColor(SheepQuality.Blue)));
        Advance(view, 4f);
        Advance(view, 0.2f);
        Assert.That(Get<bool>(view, "showing"), Is.False);
    }

    [Test]
    public void GreenAndBlueFirstDiscoveriesBothUseQualityDetailCards()
    {
        SheepDiscoveryToastView view = CreateDiscoveryView();
        SheepDetailCardView card = view.GetComponent<SheepDetailCardView>();
        Outline outline = view.GetComponent<Outline>();

        view.Show("green", Entry("green", "绿羊", SheepQuality.Green), 1);
        view.Show("blue", Entry("blue", "蓝羊", SheepQuality.Blue), 1);
        Advance(view, 0.2f);

        Assert.That(card.SheepNameText.text, Is.EqualTo("绿羊"));
        Assert.That(outline.effectColor, Is.EqualTo(SheepCardView.GetQualityColor(SheepQuality.Green)));

        Advance(view, 3f);
        Advance(view, 0.2f);

        Assert.That(card.SheepNameText.text, Is.EqualTo("蓝羊"));
        Assert.That(outline.effectColor, Is.EqualTo(SheepCardView.GetQualityColor(SheepQuality.Blue)));
    }

    [Test]
    public void PauseHidesNotificationsWithoutConsumingTheirRemainingTime()
    {
        JoinToastView join = CreateJoinView();
        SheepDiscoveryToastView discovery = CreateDiscoveryView();
        join.Show("伙伴");
        discovery.Show("rare", Entry("rare", "紫羊", SheepQuality.Purple), 1);
        Advance(join, 0.3f);
        Advance(discovery, 0.3f);
        Time.timeScale = 0f;
        Invoke(join, "Update");
        Invoke(discovery, "Update");
        Assert.That(join.GetComponent<CanvasGroup>().alpha, Is.Zero);
        Assert.That(discovery.GetComponent<CanvasGroup>().alpha, Is.Zero);
        Assert.That(Get<float>(discovery, "age"), Is.EqualTo(0.3f));
        Time.timeScale = 1f;
        Invoke(discovery, "Update");
        Assert.That(discovery.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0f));
    }

    [Test]
    public void DiscoveryPopupReusesDetailCardAtBottomLeftAndDoesNotInterceptClicks()
    {
        Sprite sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        try
        {
            JoinToastView join = CreateJoinView(sprite);
            SheepDiscoveryToastView discovery = CreateDiscoveryView(sprite);
            join.Show("伙伴");
            discovery.Show("gold", Entry("gold", "金羊", SheepQuality.Gold, sprite), 1);
            Advance(discovery, 0.2f);
            SheepDetailCardView popupCard = discovery.GetComponent<SheepDetailCardView>();
            Assert.That(discovery.GetComponent<Image>().sprite, Is.SameAs(sprite));
            Assert.That(popupCard.SheepImage.sprite, Is.SameAs(sprite));
            foreach (Graphic graphic in discovery.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(((RectTransform)join.transform).anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(((RectTransform)discovery.transform).anchorMax, Is.EqualTo(Vector2.zero));
            Assert.That(((RectTransform)discovery.transform).anchoredPosition.y, Is.GreaterThan(28f));
            Assert.That(discovery.transform.localScale.x, Is.GreaterThan(0.72f));
            Assert.That(discovery.GetComponent<Shadow>(), Is.Not.Null);
            Assert.That(discovery.GetComponent<Outline>().effectColor,
                Is.EqualTo(SheepCardView.GetQualityColor(SheepQuality.Gold)));
        }
        finally
        {
            Object.DestroyImmediate(sprite);
        }
    }

    [Test]
    public void ExistingCollectionUnlockSuppressesCelebrationAcrossNewViews()
    {
        FieldInfo singleton = typeof(SheepCollectionManager).GetField("<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);
        object previous = singleton.GetValue(null);
        SheepCollectionDatabase database = ScriptableObject.CreateInstance<SheepCollectionDatabase>();
        try
        {
            database.sheepList.Add(new SheepCollectionEntry
                { sheepId = MvpSheepCatalog.DefaultTypeId, displayName = "普通羊" });
            SheepCollectionManager manager = Child("Collection").AddComponent<SheepCollectionManager>();
            manager.database = database;
            singleton.SetValue(null, manager);
            AlphaFlockExpansionController controller = Child("Controller").AddComponent<AlphaFlockExpansionController>();
            Set(controller, "initialized", true);
            Set(controller, "stats", new AlphaRunStats());
            Set(controller, "sheepSpawner", Child("Spawner").AddComponent<ProgressiveSheepSpawner>());
            JoinToastView join = CreateJoinView();
            Set(controller, "joinToastView", join);
            SheepDiscoveryToastView first = CreateDiscoveryView();
            Set(controller, "discoveryToastView", first);
            GameObject sheepObject = Child("Sheep");
            SheepIdentity identity = sheepObject.AddComponent<SheepIdentity>();
            identity.AssignName("棉花糖");
            RecruitableSheep sheep = sheepObject.AddComponent<RecruitableSheep>();
            Invoke(controller, "HandleSheepRecruited", sheep, 1);
            Advance(first, 0.2f);
            Assert.That(Get<bool>(first, "showing"), Is.True);

            // Supply saved progress in memory; never write or clear the player's PlayerPrefs.
            Set(manager, "progressList", new List<SheepCollectionProgress>
            {
                new SheepCollectionProgress { sheepId = identity.SheepTypeId, unlocked = true, encounterCount = 1 }
            });
            SheepDiscoveryToastView nextRun = CreateDiscoveryView();
            Set(controller, "discoveryToastView", nextRun);
            Invoke(controller, "HandleSheepRecruited", sheep, 2);
            Advance(nextRun, 0.2f);
            Assert.That(Get<bool>(nextRun, "showing"), Is.False);
            Assert.That(join.GetComponentsInChildren<Image>().Length, Is.EqualTo(2));
        }
        finally
        {
            singleton.SetValue(null, previous);
            Object.DestroyImmediate(database);
        }
    }

    [Test]
    public void RarerDiscoveriesHaveLongerHoldTimes()
    {
        float previous = 0f;
        foreach (SheepQuality quality in System.Enum.GetValues(typeof(SheepQuality)))
        {
            float duration = SheepDiscoveryToastView.GetHoldDuration(quality);
            Assert.That(duration, Is.GreaterThan(previous));
            previous = duration;
        }
    }

    private GameObject Child(string name)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(root.transform, false);
        return child;
    }

    private JoinToastView CreateJoinView(Sprite sprite = null)
    {
        JoinToastView view = Child("Join").AddComponent<JoinToastView>();
        view.ConfigureStack(sprite);
        return view;
    }

    private SheepDiscoveryToastView CreateDiscoveryView(Sprite panelSprite = null)
    {
        Image panel = MvpUiFactory.CreateImage("DetailTemplate", root.transform,
            panelSprite != null ? Color.white : MvpUiFactory.Paper);
        panel.sprite = panelSprite;
        panel.rectTransform.sizeDelta = new Vector2(470f, 570f);
        Image sheepImage = MvpUiFactory.CreateImage("SheepImage", panel.transform, Color.white);
        TMP_Text name = MvpUiFactory.CreateText("Txt_SheepName", panel.transform, string.Empty, 30f);
        TMP_Text rarity = MvpUiFactory.CreateText("Txt_Rarity", panel.transform, string.Empty, 22f);
        TMP_Text count = MvpUiFactory.CreateText("Txt_Count", panel.transform, string.Empty, 18f);
        TMP_Text description = MvpUiFactory.CreateText("Txt_Description", panel.transform, string.Empty, 18f);
        TMP_Text abilityName = MvpUiFactory.CreateText("Txt_AbilityName", panel.transform, string.Empty, 20f);
        TMP_Text ability = MvpUiFactory.CreateText("Txt_AbilityDescription", panel.transform, string.Empty, 18f);
        SheepDetailCardView template = panel.gameObject.AddComponent<SheepDetailCardView>();
        template.Configure(sheepImage, name, rarity, count, description, abilityName, ability, null);
        panel.gameObject.SetActive(false);
        return SheepDiscoveryToastView.Create(root.transform, template, panelSprite);
    }

    private static SheepCollectionEntry Entry(
        string id,
        string name,
        SheepQuality quality,
        Sprite icon = null)
    {
        return new SheepCollectionEntry
        {
            sheepId = id,
            displayName = name,
            quality = quality,
            rarityName = SheepCardView.GetRarityName(quality, null),
            icon = icon,
            description = "测试描述",
            abilityName = "测试能力",
            abilityDescription = "测试能力描述"
        };
    }

    private static void Advance(object view, float seconds) => Invoke(view, "Advance", seconds);
    private static void Invoke(object target, string name, params object[] arguments) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    private static T Get<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
