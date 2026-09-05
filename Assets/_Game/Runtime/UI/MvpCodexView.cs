using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MvpCodexView : MonoBehaviour
{
    private FlockController flockController;
    private TMP_Text titleText;
    private TMP_Text listText;
    private TMP_Text detailText;
    private Button rosterTab;
    private Button catalogTab;
    private Action onBack;
    private bool showingCatalog;

    public bool IsOpen => gameObject.activeSelf;

    public static MvpCodexView CreatePlaceholder(Transform parent, FlockController flockController)
    {
        Image dimmer = MvpUiFactory.CreateImage(
            "MvpCodexPanel", parent, new Color(0f, 0f, 0f, 0.72f));
        MvpUiFactory.Stretch(dimmer.rectTransform);

        Image window = MvpUiFactory.CreateImage("Window", dimmer.transform, MvpUiFactory.Paper);
        MvpUiFactory.Anchor(
            window.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(980f, 650f));

        TMP_Text title = MvpUiFactory.CreateText(
            "Title", window.transform, "同伴名册", 40f, TextAlignmentOptions.Left);
        MvpUiFactory.Anchor(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -38f),
            new Vector2(220f, 60f));

        TMP_Text list = MvpUiFactory.CreateText(
            "List", window.transform, string.Empty, 26f, TextAlignmentOptions.TopLeft);
        MvpUiFactory.Anchor(
            list.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(40f, -20f),
            new Vector2(410f, 440f));

        Image divider = MvpUiFactory.CreateImage(
            "Divider", window.transform, new Color(0.55f, 0.52f, 0.42f, 0.65f));
        MvpUiFactory.Anchor(
            divider.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-20f, -15f),
            new Vector2(2f, 450f));

        TMP_Text detail = MvpUiFactory.CreateText(
            "Details", window.transform, string.Empty, 27f, TextAlignmentOptions.TopLeft);
        MvpUiFactory.Anchor(
            detail.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-40f, -20f),
            new Vector2(390f, 440f));

        Button roster = MvpUiFactory.CreateButton(
            "RosterTab", window.transform, "当前同伴", null, new Vector2(190f, 56f));
        MvpUiFactory.Anchor(
            roster.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(-110f, -54f),
            new Vector2(190f, 56f));

        Button catalog = MvpUiFactory.CreateButton(
            "CatalogTab", window.transform, "羊图鉴", null, new Vector2(190f, 56f));
        MvpUiFactory.Anchor(
            catalog.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(100f, -54f),
            new Vector2(190f, 56f));

        Button back = MvpUiFactory.CreateButton(
            "BackButton", window.transform, "返回", null, new Vector2(180f, 60f));
        MvpUiFactory.Anchor(
            back.GetComponent<RectTransform>(),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-115f, 48f),
            new Vector2(180f, 60f));

        MvpCodexView view = dimmer.gameObject.AddComponent<MvpCodexView>();
        view.flockController = flockController;
        view.titleText = title;
        view.listText = list;
        view.detailText = detail;
        view.rosterTab = roster;
        view.catalogTab = catalog;
        roster.onClick.AddListener(view.ShowRoster);
        catalog.onClick.AddListener(view.ShowCatalog);
        back.onClick.AddListener(view.Back);
        dimmer.gameObject.SetActive(false);
        return view;
    }

    public void Show(Action backAction)
    {
        onBack = backAction;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ShowRoster();
        rosterTab.Select();
    }

    public void Hide() => gameObject.SetActive(false);

    private void ShowRoster()
    {
        showingCatalog = false;
        titleText.text = "同伴名册";
        Refresh();
    }

    private void ShowCatalog()
    {
        showingCatalog = true;
        titleText.text = "羊图鉴";
        Refresh();
    }

    private void Refresh()
    {
        if (showingCatalog)
        {
            MvpSheepCatalogEntry entry = MvpSheepCatalog.Entries[0];
            listText.text = $"已登记类型（{MvpSheepCatalog.Entries.Count}）\n\n● {entry.DisplayName}";
            detailText.text =
                $"ID：{entry.Id}\n" +
                $"名称：{entry.DisplayName}\n" +
                $"稀有度：{entry.RarityLevel} / {entry.RarityName}\n" +
                $"携带分数：{entry.Score}\n" +
                $"动画组：{entry.AnimationGroupName}\n\n" +
                "当前 MVP 使用同一种羊外观。图鉴按内容 ID 展示，场景中同类型的多个实例不会重复生成条目。";
            return;
        }

        StringBuilder rows = new();
        StringBuilder details = new();
        int count = flockController != null ? flockController.MemberCount : 0;

        if (flockController != null)
        {
            for (int i = 0; i < flockController.Members.Count; i++)
            {
                SheepMember member = flockController.Members[i];
                if (member == null)
                    continue;

                SheepIdentity identity = member.GetComponent<SheepIdentity>();
                string displayName = identity != null && !string.IsNullOrWhiteSpace(identity.DisplayName)
                    ? identity.DisplayName
                    : "未命名小羊";
                MvpSheepCatalogEntry type = MvpSheepCatalog.Find(
                    identity != null ? identity.SheepTypeId : MvpSheepCatalog.DefaultTypeId);
                rows.Append(i == 0 ? "★ " : "• ");
                rows.Append(displayName).Append(" / ").Append(type.DisplayName).Append("\n\n");
            }
        }

        if (rows.Length == 0)
            rows.Append("当前没有同伴");

        details.Append("当前成员：").Append(count).Append("\n\n");
        details.Append("★ 为本局初始领队\n");
        details.Append("每一行代表一个真实羊实例；同类型的羊仍按各自随机别名区分。\n\n");
        details.Append("招募或成员变化后重新打开会读取最新名单。");

        listText.text = rows.ToString();
        detailText.text = details.ToString();
    }

    private void Back()
    {
        Hide();
        Action callback = onBack;
        onBack = null;
        callback?.Invoke();
    }
}
