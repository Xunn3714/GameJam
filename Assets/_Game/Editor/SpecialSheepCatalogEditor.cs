using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(SpecialSheepCatalog))]
public sealed class SpecialSheepCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SpecialSheepCatalog catalog = (SpecialSheepCatalog)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("普通羊概率", $"{catalog.CommonProbabilityPercent:0.###}%");
        if (catalog.BaseSheepPrefab == null)
            EditorGUILayout.HelpBox("缺少共用基础羊 Prefab，运行时不能生成羊。", MessageType.Error);
        if (!AssetDatabase.IsValidFolder(catalog.SourceRootFolder))
            EditorGUILayout.HelpBox($"特殊羊根目录不存在：{catalog.SourceRootFolder}", MessageType.Error);
        if (catalog.SpecialProbabilityPercent > 100.0001f)
            EditorGUILayout.HelpBox("特殊品质概率总和不能超过 100%。", MessageType.Error);

        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            if (tier == null)
                continue;

            int spawnableCount = 0;
            int missingCount = 0;
            foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
            {
                if (entry == null)
                    continue;
                if (entry.CanSpawn) spawnableCount++;
                else if (entry.Sprite == null) missingCount++;
            }

            if (tier.ProbabilityPercent > 0f && spawnableCount == 0)
            {
                EditorGUILayout.HelpBox(
                    $"{tier.FolderName} 概率为 {tier.ProbabilityPercent:0.###}%，但没有可刷新的羊；命中时会回退为普通羊。",
                    MessageType.Error);
            }
            if (missingCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{tier.FolderName} 有 {missingCount} 条记录缺少 PNG 引用，已保留 ID 并禁用刷新。",
                    MessageType.Warning);
            }
        }

        if (GUILayout.Button("从品质文件夹刷新羊种"))
        {
            SpecialSheepCatalogEditorUtility.Synchronize(catalog, saveAssets: true);
            Repaint();
        }

        if (GUILayout.Button("写入新版图鉴介绍"))
        {
            int updated =
                SpecialSheepCatalogEditorUtility.ApplyUpdatedCodexDescriptions(catalog);

            Debug.Log($"新版羊图鉴介绍已写入：{updated} 条", catalog);
            Repaint();
        }
    }
}

/// <summary>创建并同步当前 Alpha 唯一的特殊羊目录资产。</summary>
public static class SpecialSheepCatalogEditorUtility
{
    public const string CatalogFolder = "Assets/_Game/Content/Data/Sheep";
    public const string CatalogPath = CatalogFolder + "/SpecialSheepCatalog.asset";
    public const string DefaultBaseSheepPrefabPath =
        "Assets/_Game/Content/Perfabs/Sheep/RecruitableSheep.prefab";

    public static int ApplyUpdatedCodexDescriptions(SpecialSheepCatalog catalog)
    {
        if (catalog == null)
            return 0;

        int updated = 0;
        int total = 0;

        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            if (tier == null)
                continue;

            foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
            {
                if (entry == null)
                    continue;

                total++;

                string description = GetUpdatedDescription(entry.DisplayName);

                if (string.IsNullOrWhiteSpace(description))
                {
                    Debug.LogWarning(
                        $"没有找到新版图鉴文案：{entry.DisplayName} | {entry.TypeId}",
                        catalog);
                    continue;
                }

                entry.EditorSetCodexDescription(description);
                updated++;
            }
        }

        if (updated > 0)
        {
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        Debug.Log(
            $"图鉴文案检查完成：Catalog 共 {total} 条，成功匹配 {updated} 条，未匹配 {total - updated} 条。",
            catalog);

        return updated;
    }

    private static readonly Dictionary<string, string> ShortCodexDescriptions =
        new(StringComparer.Ordinal)
        {
            ["蛋小羊"] = "裹着蛋壳闯草原，走路总是咔哒响。",
            ["短短羊"] = "尾巴、耳朵和叫声都短短的。",
            ["矮脚羊"] = "腿虽短，追起队伍一点也不慢。",
            ["羊了个羊"] = "闯过第二关后，它选择保持沉默。",
            ["眼睛羊"] = "厚眼镜后藏着一张活地图。",
            ["棕羊"] = "像热可可一样暖和香甜。",
            ["胡子羊"] = "胡子很长，年纪其实并不大。",
            ["胖羊"] = "圆滚滚的身子，落地自带震感。",
            ["绅士羊"] = "戴着单片眼镜，喝露水也讲礼仪。",
            ["长角羊"] = "顶角常胜，最怕狭窄的门。",
            ["斑点羊"] = "把圆斑点当作最新款睡衣。",
            ["瘦羊"] = "只吃草尖，风大时容易站不稳。",
            ["长尾羊"] = "长尾巴扫过草地，像一把柔软扫帚。",
            ["微博羊"] = "红围脖一年四季都不离身。",
            ["奶牛羊"] = "坚信自己是奶牛，天天排队等挤奶。",
            ["吐舌头羊"] = "爱做鬼脸，尝甜草尤其灵敏。",
            ["大头羊"] = "脑袋大大的，主意也特别多。",
            ["杀马特羊"] = "彩虹发型迎风立，出场自带焦点。",
            ["羊可羊乐"] = "什么事都能乐，是羊群的开心果。",
            ["拐杖羊"] = "拐杖上的每道磨痕都是一段旅程。",
            ["爆炸羊"] = "脾气一点就炸，三秒后又忘光。",
            ["大眼羊"] = "圆眼睛太会卖萌，总能多讨一把草。",
            ["呆呆羊"] = "反应慢三拍，羊毛却格外柔软。",
            ["长毛羊"] = "羊毛像长裙，梳一次要一个下午。",
            ["秃羊"] = "头顶亮得反光，蚊子落下也打滑。",
            ["黑羊"] = "夜晚捉迷藏时几乎无人能找到。",
            ["木桶羊"] = "住在木桶里，连自己也忘了原因。",
            ["方羊"] = "身体方方正正，排队从不站歪。",
            ["铃铛羊"] = "走路叮当响，捉迷藏从没赢过。",
            ["草羊"] = "头顶嫩草，风来时摇得最欢。",
            ["花羊"] = "头顶小花，走到哪里都带着香气。",
            ["斗鸡眼羊"] = "两眼分工，左右草堆都不放过。",
            ["山羊"] = "混进羊群的山羊，咩声还在练习。",
            ["领结羊"] = "大蝴蝶结每天都要照水塘检查。",
            ["礼帽羊"] = "见面先脱帽，再郑重地咩一声。",
            ["纸盒羊"] = "纸盒是帽子、房子，也是它的安全感。",
            ["泡泡羊"] = "心情会变成不同颜色的泡泡。",
            ["派对羊"] = "彩灯一亮，它就能把草原变成舞池。",
            ["气球羊"] = "羊毛轻得会飘，出门要拴小石头。",
            ["蘑菇羊"] = "雨后头顶蘑菇会撑成一把小伞。",
            ["水母羊"] = "半透明又会发光，像游动的月光。",
            ["骂骂咧咧羊"] = "嘴上不停抱怨，手里的活却全做完。",
            ["哭哭羊"] = "开心难过都会哭，泪水能滋养青草。",
            ["雪花羊"] = "毛尖凝着雪花，总比冬天早一步。",
            ["钱袋羊"] = "能听见草丛里每一枚硬币的声音。",
            ["勇者羊"] = "危险往哪来，它就往哪冲。",
            ["壮羊羊"] = "一肩扛起草垛，是羊群的力量担当。",
            ["披着狼皮的羊"] = "想潜入狼群，却总被咩声暴露。",
            ["红内裤羊"] = "相信红内裤能带来神秘力量。",
            ["雪地羊"] = "越是天寒地冻，精神越好。",
            ["臭臭羊"] = "气味很有存在感，蚊虫都主动绕路。",
            ["羊索"] = "迎着大风站岗，嘴里总念面对疾风。",
            ["彩虹羊"] = "走过之处会留下一道七彩尾迹。",
            ["大羊叫"] = "一声咩能传过三座山谷。",
            ["九尾羊"] = "九条彩尾展开时，草原也亮了起来。",
            ["美羊羊"] = "从狼堡回家后，羊毛一直闪着光。",
            ["抑郁羊"] = "躲在暗处，只有温柔的光能让它安心。",
            ["粑粑羊"] = "走到哪拉到哪，自己却毫无察觉。",
            ["吉米羊"] = "草原上最具压迫感的重型装甲小羊，光是停在那里就足以把旁边的小羊衬得格外渺小。虽然行动不算灵活，但正面冲过来的时候，几乎没有谁愿意挡在它前面。\n\n" +
                     "\"大家第一次见它都只顾着看那层厚装甲，\"后勤羊说，\"只有我注意到它开了半天就趴窝了——它从驾驶舱里探出头，很平静地说：'这不是故障，是战术性维护。'\"",
            ["羊羊"] = "羊羊看着这些和自己不在一个次元的卑微羊类陷入了沉默。",
            ["里羊 1"] = "一只独特的里羊 1。",
            ["里羊"] = "\"你上一次刷牙是什么时候？\"\n\n" +
                     "\"你是个调查员对吧，那就调查啊，\"维克托说，\"该是你接受治疗的时候了。\"",
            ["SUPERlibersheep_KSLJ"] = "该言论涉及叛羊，正在接受真理咩调查",
            ["安吉羊"] = "圆滚滚的粉色卷毛小羊，羊毛带有白色螺旋纹路，黑脸蛋配蓝色蝴蝶结，穿着粉白色鞋。",
            ["W1K羊"] = "总是很忙且爱装冷脸的臭屁大王",
            ["whilist"] = "随心所欲的羊羊，喜欢听歌。oh~man",
            ["模拟山羊"] = "比狼敢不敢换个游戏跟我solo",
            ["Darcy"] = "darrrrrrrrrcy",
            ["三水羊"] = "为什么羴叠在一起没有变成两星？原来是时间不够没合上"
        };

    private static string GetUpdatedDescription(string displayName)
    {
        string normalizedName = displayName?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedName)
            && ShortCodexDescriptions.TryGetValue(normalizedName, out string description))
        {
            return description;
        }

        return GetLegacyDescription(displayName);
    }

    private static string GetLegacyDescription(string displayName)
    {
        switch (displayName?.Trim())
        {
        // =========================
        // 常规小羊
        // =========================

        case "礼帽羊":
        case "礼帽小羊":
            return "头戴一顶笔挺的小礼帽，是草原上最讲礼仪的小羊，见人先脱帽，再道一声\"咩\"。\n\n" +
                   "\"草原上的小狗们有个不成文的规矩：礼帽小羊路过时，要排好队让它挨个视察，\"牧羊犬说，\"不是怕它，是怕不回礼的话，它会一直举着帽子站在原地，大家都尴尬。\"";

        case "领结羊":
        case "蝴蝶结羊":
        case "蝴蝶结小羊":
            return "头上系着大大的蝴蝶结，最爱漂亮，整天在水塘边照镜子，欣赏自己的新蝴蝶结。\n\n" +
                   "\"水塘的水中午最清，可蝴蝶结小羊偏要傍晚来照，\"小青蛙说，\"它自己说的，夕阳能给蝴蝶结加一层滤镜——这个秘密我替它保守了一整个夏天。\"";

        case "山羊":
            return "其实是一只混进羊群的山羊，却坚称自己是羊群的一份子，大家也都不忍心拆穿它。\n\n" +
                   "\"我数过三次羊群，每次数都多出一只，\"牧羊人说，\"后来我凑近了看——那一只正踮着后腿站在队尾，练习咩叫，嗓音还有点劈。\"";

        case "黑羊":
        case "黑色小羊":
            return "羊群里独一无二的小黑羊，夜晚捉迷藏的隐藏王者——灯一关，谁也找不到它。\n\n" +
                   "\"晚上捉迷藏我们从不找黑色小羊，\"白羊说，\"不是找不到，是上回有人黑夜里一屁股坐在它身上，被它讲了半个小时的'捉迷藏礼仪'。\"";

        case "矮脚羊":
        case "矮脚小羊":
            return "腿短短的小羊，别人走一步它要走三步，不过吃起草来可一点不落后。\n\n" +
                   "\"这片草原的草长得特别均匀，\"割草工说，\"后来我看见矮脚小羊的脚印才明白——一步顶三步的密度，一口草都没落下，比我割得还整齐。\"";

        case "拐杖羊":
        case "拐杖小羊":
            return "拄着拐杖的长者，走过南闯过北，脸上的每道皱纹里都藏着一个草原故事。\n\n" +
                   "\"你们小羊总爱比谁走的路多，\"拐杖说，\"我身上的这些磨痕才是它走过的里程——一道痕一个故事，想听的话，得带草来换。\"";

        case "胖羊":
            return "圆滚滚、毛茸茸，是草原上伙食最好的小羊，蹦一下地面都要抖三抖。\n\n" +
                   "\"牧场的地震仪隔几天就报一次警，\"牧场主说，\"维修工来检查了两回，最后把仪器搬走了：机器没坏，是隔壁胖羊又蹦了一下。\"";

        case "瘦羊":
            return "挑食只吃草尖尖，身材苗条得让风都担心把它吹跑。\n\n" +
                   "\"草原上的风其实性子很急，\"蒲公英说，\"但每次路过瘦羊身边，它都会屏住呼吸、踮着脚走——生怕把这位客人给吹跑了。\"";

        case "斑点羊":
            return "一身圆斑点，自称穿的是\"最新款睡衣\"，吃得越多，斑点越圆。\n\n" +
                   "\"我负责全羊群的洗衣活儿，可从没洗过它那件'睡衣'，\"洗衣羊大婶说，\"不过我记着账呢：它每吃完一顿饭，我就得在本子上多描一个圈——这个月的圈，描得特别圆。\"";

        case "长角羊":
        case "大角羊":
            return "一对威武的弯角，顶角比赛常胜将军，唯一的烦恼是过不了窄门。\n\n" +
                   "\"牧场的窄门已经加宽过三次了，\"木匠说，\"每次我问是谁撞的门框，都没羊承认——只有大角羊在我身后仰头看天，角还卡在门框上没拔出来。\"";

        case "奶牛羊":
            return "黑白花纹相间，总以为自己是头奶牛，每天排队等着牧场主来\"挤奶\"。\n\n" +
                   "\"挤奶房的桶天天是空的，可门外的队伍天天排到草垛，\"牧场主说，\"我凑近听过——那队'奶牛'等着被挤的时候，一个个咩得可认真了。\"";

        case "秃羊":
            return "头顶光滑锃亮，是草原上最\"滑头\"的小羊，蚊子落上去都要打滑。\n\n" +
                   "\"夏天草原上的蚊子最多，可秃羊头顶一口都没被叮过，\"小蚊子说，\"不是不想叮——上回我刚落脚就滑了出去，在空中转了三圈，同伴们笑我到今天。\"";

        case "花羊":
        case "花花羊":
            return "头顶开着一朵小花，走到哪里都香香的，和草草羊是形影不离的好朋友。\n\n" +
                   "\"花花羊路过的时候，我们的工蜂会跟着飞出半里地，\"蜜蜂队长说，\"别误会，不是采蜜——是去确认它头顶那朵花，今天有没有开出新品种。\"";

        case "草羊":
        case "草草羊":
            return "头顶一株嫩绿小草，是羊群里最有生机的小羊，风吹过来还会跟着摇一摇。\n\n" +
                   "\"我一度以为草原出了怪事：无风的日子，草也在摇，\"牧羊人说，\"后来我站上高坡才看清——摇得最起劲的是草草羊头顶那株，周围的草都在跟它学。\"";

        case "眼镜羊":
            return "架着一副厚底眼镜，最爱看书，据说草原上每一张地图它都背得下来。\n\n" +
                   "\"新来的羊总问我草原地图在哪儿，\"图书管理员说，\"我直接指眼镜羊——比地图准，还带更新，就是问路时它要先扶一扶眼镜，再给你讲一路的第三条近道。\"";

        case "胡子羊":
            return "一把长胡子垂到胸口，小羊们见了都喊\"爷爷\"，其实它只是胡子长得快。\n\n" +
                   "\"我们喊它爷爷，不是因为它老，\"小羊说，\"上回有人喊了声'叔叔'，它当场沉默着量了量自己的胡子，量了整整十分钟——从那以后，大家都喊对了。\"";

        case "爆炸头羊":
            return "一头蓬松爆炸卷，是草原上最潮的仔，雷雨天过后头发会更蓬。\n\n" +
                   "\"我报错过一次天气，\"气象羊说，\"我说有一朵雷云停在草原上不肯走——全草原的羊都跑来看，结果是爆炸头羊雨后的脑袋，比云还蓬。\"";

        case "围脖羊":
            return "一年四季围着红围脖，是羊群中最有仪式感的小羊，围脖是奶奶亲手织的。\n\n" +
                   "\"草原夏天不下雪，可围脖羊的脖子永远是暖红色的，\"小羊说，\"奶奶每年织一条新的，旧的它说'在衣柜里休假'——一年休一条，衣柜快休满了。\"";

        case "羊了个羊":
            return "名字本身就是个谜的神秘小羊，据说挑战过它第二关的羊，都沉默了。\n\n" +
                   "\"我不知道它的第二关是什么，也不想知道，\"挑战过的羊说，\"我只知道那天出来以后，我沉默着吃了三天草，现在看见'羊'字都要先闭一下眼。\"";

        case "长毛羊":
            return "羊毛长得拖到地上，走起路来像穿了一条长裙，梳一次毛要整整一个下午。\n\n" +
                   "\"我不是梳子，我是加班工，\"梳子说，\"每次在长毛羊头上上岗是正午，下岗是日落——它还会问我：'要不要再梳一遍？'\"";

        case "杀马特羊":
            return "彩虹色竖立羊毛，草原贵族家族的末代掌门人，座右铭：别爱我没结果。\n\n" +
                   "\"草原上有一道彩虹，雨后从来不消失，\"摄影羊说，\"我趴了三天草窝想拍它——第四天它转了个身，我才明白那是杀马特羊出门散步。\"";

        case "棕羊":
            return "可可色的羊毛，闻起来有淡淡的巧克力味，冬天大家都爱挨着它取暖。\n\n" +
                   "\"冬天牧场栅栏边最挤，我怎么驱都驱不散，\"牧羊犬说，\"后来我闻明白了——大家挤在棕羊身边，说它闻起来像一块正在化开的热巧克力。\"";

        case "大眼羊":
            return "一双圆眼睛占了半张脸，卖萌高手，看它一眼就忍不住多喂一把草。\n\n" +
                   "\"牧场的草料预算总是超支，财务大婶查了半年，\"牧场主说，\"结论是没羊偷吃——是每只路过的羊都忍不住多喂大眼羊一把草，这账，怪它眼睛太圆。\"";

        case "斗鸡眼羊":
            return "两只眼睛各看一边，能同时盯住左右两片草堆，干饭从不错过。\n\n" +
                   "\"左边的小羊和右边的小羊曾为斗鸡眼羊到底在看谁吵过一架，差点绝交，\"裁判羊说，\"后来真相大白——它看的是它俩身后的两片草堆，谁也没看。\"";

        case "短短羊":
            return "不光个子短，尾巴短、耳朵短，连咩叫声都比别人短半拍。\n\n" +
                   "\"合唱团的'咩'声部总是提前半拍收尾，我批评过全团偷懒，\"指挥说，\"后来我逐一对了谱子，冤枉大家了——短短羊的那一声本来就只有那么长，它已经唱满了。\"";

        case "吐舌头羊":
        case "吐舌羊":
            return "总是吐着舌头做鬼脸，是羊群里的开心果，尝起甜草来舌头格外灵活。\n\n" +
                   "\"牧场边的甜草丛总是最先被吃干净，我蹲守多次都没抓到现场，\"园丁羊说，\"其实不用蹲——太阳底下谁的舌头吐得最长最亮，就是谁，它还在冲我做鬼脸。\"";

        case "铃铛羊":
            return "脖子上挂着一枚小铜铃，走起路来叮叮当当，捉迷藏时总是第一个被找到。\n\n" +
                   "\"草原捉迷藏新增了规则：铃铛羊每轮先当抓人的，\"小羊说，\"不是针对它——上回它躲进草垛，铃铛响了一个钟头，大家趴在草里谁也不好意思出声。\"";

        case "绅士羊":
            return "带着一副单眼镜片，每天清晨优雅地啜饮露水，是草原上最讲究的老派绅士。\n\n" +
                   "\"清晨的露水其实到处都有，可绅士羊偏要用叶子盛着喝，\"小羊说，\"喝完还要对叶子行个礼——叶子抖了一下，没敢不受，我们也没敢不学。\"";

        case "木桶羊":
        case "桶装羊":
            return "住在一只小木桶里，只露出一个脑袋，谁也不知道为什么，它自己也忘了。\n\n" +
                   "\"那只桶的木头我每年换一回，桶里的羊倒是从没换过，\"木匠说，\"我问过它为什么住里面，它想了很久：'我也忘了，但里面这张床，确实舒服。'\"";

        case "长尾羊":
            return "尾巴长得拖在身后，走路顺便把地扫得干干净净。\n\n" +
                   "\"牧场的小路清晨最干净，我也终于能睡个懒觉了，\"清洁工说，\"别误会，不是我偷懒——长尾羊晨练走一圈，比我扫得还彻底，我只负责查漏补缺。\"\n\n" +
                   "收集加成：集齐 9 条长尾巴，唤醒传说中的\"九尾羊\"。";

        case "呆呆羊":
            return "总是呆呆地望着远方，叫它要反应三秒，但羊毛是羊群里最蓬最软的。\n\n" +
                   "\"叫呆呆羊要连叫三声，\"牧羊人说，\"第一声是通知，第二声是等它缓冲，第三声它才回头——但回头那一下值得等，那身羊毛，摸过的都说值。\"";

        case "大头羊":
            return "脑袋大得不成比例，是羊群里的智多星，毕竟脑容量摆在那里。\n\n" +
                   "\"羊群遇到难题的时候，大家会先去看大头羊的头，\"小羊说，\"不是围观——是只要那颗头开始缓缓转动，就说明有办法了，全体都能松口气。\"";

        case "方羊":
            return "身体方方正正，像积木拼出来的，是草原上最规整的小羊，排队是它的天赋。\n\n" +
                   "\"牧场的队伍从不需要整队员，\"牧羊人说，\"只要方羊往队里一站，前后自动对齐——连风路过都要绕着走，不敢吹歪它。\"";


        // =========================
        // 水果系列小羊
        // =========================

        case "椰子小羊":
            return "来自热带小岛，顶着椰子壳当帽子，敲一敲会发出咚咚的闷响，据说里面还藏着甜甜的椰子水。\n\n" +
                   "\"岛上的小猴子曾想摘它头顶那颗'椰子'，\"岛民说，\"敲了三下只听见咚咚的闷响，猴子们就围着它坐下了，说等椰子熟了自己会掉——今年已经是它们守的第三年。\"";

        case "苹果羊":
            return "羊毛红润，散发着脆甜的苹果香，咬一口……不对，摸一下就知道是水果系列里最受欢迎的一款。\n\n" +
                   "\"每年采摘节的客人都会问苹果树在哪儿，说我们的苹果闻着特别香，\"牧场主说，\"我往草场一指——羊群里最红最香的那只就是，不用上树。\"";

        case "草莓羊":
            return "粉嫩的羊毛上缀着小小的籽，甜香扑鼻，走到哪里都有小蜜蜂跟着。\n\n" +
                   "\"蜂巢搬了三次家，一次比一次靠近羊群，\"蜜蜂队长说，\"不是环境不好——是草莓羊走到哪儿，我们的工蜂就'旷工'跟到哪儿，拦都拦不住。\"";

        case "香蕉羊":
            return "一身亮黄的弯月形羊毛，远远看去软糯可口，别问能不能剥皮，问就是底线问题。\n\n" +
                   "\"羊群曾为它能不能剥皮开过辩论会，官司打到我这儿，\"法官羊说，\"开庭那天香蕉羊只说了一句：'问就是底线问题。'全场安静，案子至今封存。\"";

        case "葡萄羊":
            return "圆滚滚的紫羊毛，远看就是一整串葡萄，是水果系列里最不想被\"摘\"的小羊。\n\n" +
                   "\"采摘派对上没人敢站葡萄羊旁边，\"小羊说，\"不是它脾气差——是怕自己手一抬，被当成要摘葡萄的，瞪一整晚，那眼神可圆了。\"";

        case "西瓜羊":
            return "外皮绿条纹、内心红彤彤，是夏天最解暑的小羊，拍一拍还会发出清脆的\"砰砰\"声。\n\n" +
                   "\"夏天午睡前，小羊们会排队拍一拍西瓜羊的背，\"牧场主说，\"不是欺负它——谁拍出的声音最清脆，谁就能挨着它睡午觉，这是草原抢凉席的规矩。\"";

        case "梨羊":
            return "梨形的身体、淡绿的羊毛，带着一股清甜的气味，是水果系列里最低调的成员。\n\n" +
                   "\"水果系列拍合照永远少一只，\"摄影羊说，\"不用找——梨羊又站到最后排去了，说自己是'路过'。可照片里那股清甜味儿藏不住，梨形的身子也藏不住。\"";


        // =========================
        // 新增小羊
        // =========================

        case "车公子":
            return "羊群里最有排面的富家少爷，羊毛一天要梳三遍，吃草只认最贵的\"手选特等\"。\n\n" +
                   "\"草原的草价表是它一手改写的，\"草贩羊说，\"它付双倍价钱，但有个条件——我递草的时候必须多说一句：'谢谢车公子。'\"";

        case "爆炸羊":
            return "脾气像爆竹，一点就炸，不过三秒就消气，转头就忘了自己刚才在气什么。\n\n" +
                   "\"草原的火警一天要响好几回，全是爆炸羊发脾气误触的，\"消防羊说，\"但现在没人跑了——都知道它三秒就消气，还会叼着甜草来赔不是。\"";

        case "棉花糖羊":
            return "羊毛蓬松香甜得像一团棉花糖，站着不动的时候，总有一圈小羊围着它，想咬上一口。\n\n" +
                   "\"我在集市做棉花糖多年，这半年生意惨得很，\"棉花糖贩羊说，\"不是我手艺退步——是大家都说棉花糖羊的毛更新鲜更甜，还附赠一只会眨眼的活羊。\"";

        case "雷云羊":
            return "羊群里最爱开新品发布会的小羊，开口必问\"大家还好吗\"，每一代新羊毛都要办一场盛大的发布。\n\n" +
                   "\"它的发布会讲稿有三百页，二百九十页在对比上一代羊毛，\"场务羊说，\"最后一页永远是同一行字：'这一次，我们认真了。'\"";

        case "勇者羊":
            return "羊群里最勇敢的小羊，哪里有危险就往哪里冲，字典里从没有\"撤退\"两个字。\n\n" +
                   "\"羊群的疏散演习总是反过来进行，\"牧羊犬说，\"全体往外跑，只有勇者羊往里冲——去确认还有谁没出来，每次回来都一身泥，还咧嘴笑。\"";

        case "公主羊":
            return "自认为是真公主的小羊，出门必须走最干净的那片草地，吃草只吃别人摘好递到嘴边的草尖。\n\n" +
                   "\"草原有一条红毯，只在公主羊出门时铺，\"管家羊说，\"后来我学乖了，铺一半就停——剩下那段它自己踮着脚走完，说这是在'练习仪态'。\"";

        case "同号羊":
            return "编号牌和另一只羊完全相同的小羊，这辈子还没成功证明过\"我是我\"。\n\n" +
                   "\"每次点名都是灾难现场，\"点名羊说，\"我喊七号，两只羊同时答到，声音还一模一样——后来我改按特征喊，它俩同一天开始留胡子。\"";

        case "金币羊":
            return "爱收集金币的小羊，捡到一切亮闪闪的东西都往羊毛里藏，是羊群的小小财库。\n\n" +
                   "\"草原的失物招领箱空了很久，\"失物管理员羊说，\"不是没人丢东西——是亮闪闪的全被金币羊先捡走了，它会留一张欠条：'利息：一把甜草。'\"";

        case "四叶草羊":
            return "头顶长着一株四叶草的小羊，是羊群里运气最好的羊，摔一跤都能正好摔进一片甜草丛。\n\n" +
                   "\"探路这种活儿我从不抽签，\"探险羊说，\"直接推四叶草羊出去——上回它绊了一跤，爬起来的时候，替全群发现了一处新水源。\"";

        case "披着狼皮的羊":
            return "披着狼皮外套的小羊，一心想混进狼群打探情报，可惜咩叫声每次都出卖它。\n\n" +
                   "\"狼群早就知道队伍里混进了一只羊，但谁也不拆穿，\"狼首领说，\"它半夜练狼嚎，咩出来的声音还挺可爱——我们全群都在假装没听见。\"";

        case "四尾羊":
            return "长着四条尾巴的稀有小羊，走起路来四尾齐摆，像身后有人同时挥着四面小旗。\n\n" +
                   "\"草原的旗帜节从不用做旗，\"节庆羊说，\"借四尾羊的就行——四条尾巴一齐挥，节奏还不带错的，比旗队整齐多了。\"";

        case "轮滑羊":
            return "脚踩轮滑鞋的小羊，是草原上滑行速度最快的仔，下坡的时候却从来刹不住车。\n\n" +
                   "\"草原的交通规则为它单加了一条，\"交警羊说，\"'下坡路段轮滑羊优先'——不是特权，是上回让它让路，它一路滑出三公里，停在了邻居家牧场。\"";

        case "臭臭羊":
            return "身上散发着难以描述的气味的小羊，路过之处蚊蝇绕道三里，它自己却毫无察觉。\n\n" +
                   "\"羊群的蚊虫密度图上有一块空白圆圈，\"昆虫学家羊说，\"没蚊子、没苍蝇、连蚂蚁都没有——圆心是臭臭羊，它还在纳闷大家打招呼为什么都屏住呼吸。\"";

        case "壮羊羊":
        case "壮羊":
            return "一身腱子肉的小羊，单肩能扛起一整捆干草，是羊群里的免费搬运工。\n\n" +
                   "\"搬家公司在草原丢了半个市场，\"搬家公司老板羊说，\"不是大家不搬家，是都去找壮羊——报酬一把甜草，比我们搬得又快又稳。\"";

        case "水果捞羊":
            return "羊毛混着好几种水果的颜色和香气，是水果系列的\"全家福\"款，每天闻起来都不一样。\n\n" +
                   "\"我摆水果摊多年，叫不出它身上全部的水果名，\"水果贩羊说，\"水果捞羊天天来让我鉴定——昨天是草莓，今天是菠萝，它说这叫'换口味'。\"";

        case "红内裤羊":
            return "常年穿着红内裤的小羊，坚信这样能获得神秘力量，本命年的时候尤其嚣张。\n\n" +
                   "\"草原有个传说：红内裤穿在外面就能当超级英雄，\"小羊说，\"它真试了——穿了三天又改回里面，说草原风大，有点凉。\"";


        // =========================
        // 特效小羊
        // =========================

        case "彩虹羊":
            return "走过的地方会拖出一条七彩尾迹的小羊，是草原上最闪的移动风景，阴天时大家都抬头看它的尾巴认天气。\n\n" +
                   "\"草原的天气预报早就不看云了，\"气象羊说，\"看彩虹羊的尾巴就行——颜色鲜亮是晴，颜色发灰要下雨，比我的气压计准，还不会坏。\"";

        case "粑粑羊":
            return "完全憋不住的小羊，走到哪儿拉到哪儿，其他羊都不愿意沾边，它自己却毫无察觉，还以为大家在跟它玩追逐游戏。\n\n" +
                   "\"我一直以为'拉够一千坨'只是任务板上的一个数字，\"牧场主说，\"直到那天计数满格，粑羊从地平线那头颠颠跑过来——全羊群跟在它后面数了一路，现在谁也不跟它玩捉迷藏了，太有味儿了。\"";

        case "王子羊":
            return "羊毛上燃着一圈蓝色火焰的王子羊，是从狼口里救出公主的勇者，在草原上人气仅次于公主羊本人。\n\n" +
                   "\"红毯现在要铺两条了，\"管家羊说，\"公主羊走一条，王子羊走一条——它救公主那天，蓝火把半边草原都照亮了，公主亲自给它颁的勋章，它嫌火焰太亮晃眼，勋章至今别在火焰最暗的那撮毛上。\"";

        case "大羊叫":
            return "咩叫声震天响的小羊，一声超声波\"咩\"能传出三个山谷，可它自己总爱躲在最意想不到的角落。\n\n" +
                   "\"跟它玩捉迷藏纯拼耐力，\"寻找羊说，\"蒙上眼睛，全靠那一声超声波辨位，听了七八回才锁定方向——结果它一直站在我身后，憋笑憋得尾巴直抖。\"";

        case "美羊羊":
            return "草原上最美的小羊，曾被关在狼堡里，被救出来那天羊毛亮得发光，像被重新打磨过一遍。\n\n" +
                   "\"狼堡的栅栏被撬开那天，全草原都屏住了呼吸，\"摄影羊说，\"美羊羊慢慢走出来，逆着光——那张照片至今挂在草原入口，标题就两个字：回家。\"";

        case "抑郁羊":
            return "浑身羊毛灰蒙蒙的小羊，总独自躲在丛林最暗的角落，只有一只会发光的小羊靠近时，它才肯探出一点点头。\n\n" +
                   "\"丛林里有片连阳光都绕着走的地方，就是它的家，\"带路羊说，\"我们举着发光羊一步步挪过去，光照到它脸上的那一刻，它眨了眨眼——原来它的眼睛，一直都很亮很亮。\"";

        case "盲盒羊":
            return "眼睛看不见的小羊，触觉却灵得惊人，蹄子一摸就能分出金币真假，草原上那一百枚假金币全是它挑出来的。\n\n" +
                   "\"造币厂请它去当鉴定师，开价一天一把甜草，\"厂长羊说，\"它没去——它说更喜欢自己捡假金币，捡够一百枚就觉得自己'发财了'，这是它的小快乐，我们谁也没戳破。\"";

        case "烤全羊":
            return "总爱躺在烧烤架上晒太阳的小羊，羊毛带一股淡淡的孜然味，架子底下从来没人敢点火，它自己还以为那是大家客气。\n\n" +
                   "\"草原上那台烧烤架'坏'了三年了，\"厨师羊说，\"不是坏了——是烤全羊天天躺在上面，自己翻面、自己撒盐，说这叫'腌制生活'，谁劝都不下来。\"";

        case "九尾羊":
            return "集齐九条长尾巴才能唤醒的传说之羊，九条炫彩粉色尾巴一齐展开时，像九道彩带在草原上空跳舞。\n\n" +
                   "\"第九条尾巴凑齐那天，全草原都安静了，\"长尾羊说，\"粉光从草垛里升起来的时候我闭了下眼——再睁开，它正冲我摇九条尾巴，一条一条，像在数给我们听。\"";

        case "蘑菇羊":
            return "头顶长着一朵小蘑菇的小羊，雨后蘑菇伞盖会撑得更大，是草原上闻起来最\"鲜\"的小羊。\n\n" +
                   "\"雨后我进丛林采蘑菇，回回空手，\"采菇羊说，\"不是没有——是全长在蘑菇羊头上了，它还蹲在那儿等我，伞盖撑得特别开，像在说：今天这朵，格外肥。\"";


        // =========================
        // 趣味小羊
        // =========================

        case "蛋小羊":
            return "出生时是从一颗蛋里孵出来的小羊，至今没搞清楚自己到底是羊还是蛋，睡觉时总要把自己卷成一个圆。\n\n" +
                   "\"孵蛋场的登记簿上有一行谁也划不掉的记录，\"登记员说，\"'第 37 颗蛋，孵化成功，品种：羊'——蛋小羊每年生日都来重读一遍，读完还要趴回蛋形的窝里睡一觉。\"";

        case "羊可羊乐":
            return "名字听起来就乐的小羊，无论发生什么都能笑出声，是草原上公认的开心果二号（一号是吐舌羊）。\n\n" +
                   "\"草原上最难的活儿是逗它笑，\"讲笑话的羊说，\"我准备了整整一冬的段子——结果它听完只是咧嘴，反倒我被它这一乐给逗笑了，现在我俩是搭档。\"";

        case "纸盒羊":
            return "住在一堆纸盒里的小羊，每天换一个盒子住，盒子上还用爪子画了门牌号，从 1 号排到 100 号。\n\n" +
                   "\"废品站的纸盒永远不够用，\"站长羊说，\"不是货源少——是纸羊把带窗户图案的盒子全挑走了，说'住带窗的，心情好'，我们只好给它留一摞。\"";

        case "泡泡羊":
            return "浑身能吹出彩色泡泡的小羊，生气时泡泡是灰的，开心时是七彩的，全草原的天气情绪预报就靠它。\n\n" +
                   "\"草原的孩子们最爱追着它跑，\"牧羊犬说，\"它一开心，泡泡能飘满半个草原——上回它连着乐了三天，牧场的晾衣绳上挂满了没破的泡泡，谁也不舍得戳。\"";

        case "派对羊":
            return "随时随地都在办派对的小羊，头上顶着派对帽，脖子挂着彩带，哪怕只有它一只羊在场，也要吹蜡烛许愿。\n\n" +
                   "\"草原的日历被它改过，\"日历管理员说，\"一年 365 天，它标了 400 个'派对日'——多出来的那些是'预办'和'补办'，它说派对这东西，宁可多办不能漏办。\"";

        case "气球羊":
            return "羊毛轻得像充满气的气球，风一大就要飘起来，出门得在蹄子上拴小石头，它自己倒觉得飘着挺好玩。\n\n" +
                   "\"草原的风筝线从来不够用，\"放风筝的羊说，\"都借给气球羊了——上回风太大，它飘到了邻镇，靠着一根红线被我们拽回来，落地第一句是：'上面风景真不错。'\"";

        case "水母羊":
            return "半透明的小羊，身体像水母一样软软的、会发光，夜晚在草原上飘过时，像一团游动的月光。\n\n" +
                   "\"夜班的牧羊犬从不打手电，\"牧羊犬说，\"水母羊飘在前面照路就够了——它高兴时发暖光，警惕时发冷光，比灯笼懂事，还会自己绕开水坑。\"";

        case "骂骂咧咧羊":
            return "嘴上永远不饶人的小羊，一边骂骂咧咧一边把活儿全干了，是草原上最典型的\"刀子嘴豆腐心\"。\n\n" +
                   "\"修栅栏那回它骂了我一路，\"木匠说，\"'手法糙''钉子歪''这活儿迟早塌'——骂完它自己把栅栏重新钉了一遍，钉得比我的还结实，走的时候还骂了句'下回别叫我'，第二天照来。\"";

        case "哭哭羊":
            return "动不动就掉眼泪的小羊，吃到甜草会哭，被人夸会哭，看晚霞也会哭，眼泪掉进草地里，那片草长得格外好。\n\n" +
                   "\"草原上有一片草长得比别处高半尺，\"园丁羊说，\"我研究了一个月，土壤、光照都没异常——后来发现是哭哭羊天天在那儿哭，它的眼泪，比什么肥都好使。\"";

        case "雪花羊":
            return "羊毛上结着细小的雪花纹路，走到哪里都像带着一阵冬天的气息，安安静静站着时，连呼出的气都像一小团白雾。\n\n" +
                   "\"草原一年里最先知道冬天要来的，不是气象羊，是雪花羊，\"守夜羊说，\"它往坡上一站，毛尖上就开始泛起细细的白光，第二天清晨，草尖果然全结霜了。\"";

        case "钱袋羊":
            return "腰间总挂着一个鼓鼓的钱袋，走起路来叮当作响，对亮晶晶的东西尤其敏感，连掉进草里的小铜币都能第一时间发现。\n\n" +
                   "\"失物招领箱里最常见的不是手帕，是硬币，\"失物管理员羊说，\"因为钱袋羊每天都会叼着一小堆过来，得意地抖抖袋子，像在说：'今天又小赚一笔。'\"";

        case "雪地羊":
            return "通体雪白，像刚从厚雪里打过滚出来的小羊，最喜欢在冷飕飕的地方打盹，越是天寒地冻，它越显得精神。\n\n" +
                   "\"别的羊一到冬天就往草垛里钻，雪地羊偏要往空地上跑，\"牧羊人说，\"上回下了一夜雪，第二天它在雪地里躺出一个完整的羊印子，还睡得特别香。\"";

        case "羊索":
            return "背着一把长刀、总爱迎风站着的小羊，自称是草原上的浪客，走路一定要挑风最大的方向，嘴里还总念叨着“面对疾风吧”。\n\n" +
                    "\"草原一刮大风，大家都往棚里跑，只有羊索往坡顶冲，\"牧羊犬说，\"上回它站了一下午，披风都快吹飞了还不肯下来——问它为什么，它只回了一句：'风还没停。'\"";

        case "里羊":
            return "\"你上一次刷牙是什么时候？\"\n\n" +
                "\"你是个调查员对吧，那就调查啊，\"维克托说，\"该是你接受治疗的时候了。\"";

        case "吉米羊":
            return "草原上最具压迫感的重型装甲小羊，光是停在那里就足以把旁边的小羊衬得格外渺小。虽然行动不算灵活，但正面冲过来的时候，几乎没有谁愿意挡在它前面。\n\n" +
                "\"大家第一次见它都只顾着看那层厚装甲，\"后勤羊说，\"只有我注意到它开了半天就趴窝了——它从驾驶舱里探出头，很平静地说：'这不是故障，是战术性维护。'\"";
        
        case "模拟山羊":
            return "比狼敢不敢换个游戏跟我solo \n\n";
        
        case "whilist":
            return "随心所欲的羊羊，喜欢听歌。oh~man \n\n";

        case "W1K羊":
            return "总是很忙且爱装冷脸的臭屁大王 \n\n";

        case "SUPERlibersheep_KSLJ":
            return "该言论涉及叛羊，正在接受真理咩调查 \n\n";

        case "羊羊":
            return "羊羊看着这些和自己不在一个次元的卑微羊类陷入了沉默。 \n\n";

        case "三水羊":
            return "为什么羴叠在一起没有变成两星？原来是时间不够没合上";

        case "安吉羊":
        case "安吉羊_走2":
            return "圆滚滚的粉色卷毛小羊，羊毛带有白色螺旋纹路，黑脸蛋配蓝色蝴蝶结，穿着粉白色鞋。";

        case "Darcy":
            return "darrrrrrrrrcy \n\n";

        default:
            return null;
        }
    }

    private static bool syncScheduled;

    [InitializeOnLoadMethod]
    private static void ScheduleInitialSynchronization()
    {
        ScheduleSynchronization();
    }

    [MenuItem("Game Jam/Sheep/Sync Special Sheep Catalog")]
    public static void SynchronizeFromMenu()
    {
        SpecialSheepCatalog catalog = LoadOrCreate();
        Synchronize(catalog, saveAssets: true);
        Selection.activeObject = catalog;
    }

    public static SpecialSheepCatalog LoadOrCreateAndSynchronize()
    {
        SpecialSheepCatalog catalog = LoadOrCreate();
        Synchronize(catalog, saveAssets: true);
        return catalog;
    }

    public static SpecialSheepCatalog LoadOrCreate()
    {
        SpecialSheepCatalog catalog = AssetDatabase.LoadAssetAtPath<SpecialSheepCatalog>(CatalogPath);
        if (catalog != null)
            return catalog;

        EnsureFolder(CatalogFolder);
        catalog = ScriptableObject.CreateInstance<SpecialSheepCatalog>();
        catalog.EditorEnsureDefaults();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    public static bool Synchronize(SpecialSheepCatalog catalog, bool saveAssets)
    {
        if (catalog == null)
            return false;

        string before = EditorJsonUtility.ToJson(catalog);
        catalog.EditorEnsureDefaults();
        if (catalog.BaseSheepPrefab == null)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBaseSheepPrefabPath);
            RecruitableSheep recruitable = prefabAsset != null
                ? prefabAsset.GetComponent<RecruitableSheep>()
                : null;
            if (recruitable != null)
                catalog.EditorSetBaseSheepPrefab(recruitable);
        }
        Dictionary<string, SpecialSheepCatalog.Entry> existingByGuid = new(StringComparer.Ordinal);
        Dictionary<SpecialSheepCatalog.Entry, SpecialSheepCatalog.Tier> originalTierByEntry = new();
        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            if (tier == null)
                continue;
            foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
            {
                if (entry == null)
                    continue;
                originalTierByEntry[entry] = tier;
                if (!string.IsNullOrWhiteSpace(entry.AssetGuid) && !existingByGuid.ContainsKey(entry.AssetGuid))
                    existingByGuid.Add(entry.AssetGuid, entry);
            }
        }

        Dictionary<SpecialSheepCatalog.Tier, List<SpecialSheepCatalog.Entry>> synchronized = new();
        HashSet<SpecialSheepCatalog.Entry> seen = new();
        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            if (tier == null)
                continue;

            List<SpecialSheepCatalog.Entry> entries = new();
            synchronized[tier] = entries;
            string folderPath = CombineAssetPath(catalog.SourceRootFolder, tier.FolderName);
            if (!AssetDatabase.IsValidFolder(folderPath))
                continue;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsDirectPngChild(folderPath, assetPath))
                    continue;

                NormalizeTextureImporter(assetPath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                    continue;

                if (!existingByGuid.TryGetValue(guid, out SpecialSheepCatalog.Entry entry))
                    entry = new SpecialSheepCatalog.Entry();

                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                string typeId = string.IsNullOrWhiteSpace(entry.TypeId)
                    ? ResolveStableTypeId(fileName, guid)
                    : entry.TypeId;
                entry.EditorBindImportedSprite(guid, typeId, fileName, sprite);
                entries.Add(entry);
                seen.Add(entry);
            }

            entries.Sort((left, right) => string.CompareOrdinal(left.TypeId, right.TypeId));
        }

        foreach (KeyValuePair<SpecialSheepCatalog.Entry, SpecialSheepCatalog.Tier> pair in originalTierByEntry)
        {
            if (seen.Contains(pair.Key))
                continue;

            pair.Key.EditorMarkMissing();
            synchronized[pair.Value].Add(pair.Key);
            synchronized[pair.Value].Sort((left, right) => string.CompareOrdinal(left.TypeId, right.TypeId));
        }

        foreach (KeyValuePair<SpecialSheepCatalog.Tier, List<SpecialSheepCatalog.Entry>> pair in synchronized)
        {
            pair.Key.EditorEntries.Clear();
            pair.Key.EditorEntries.AddRange(pair.Value);
        }

        string after = EditorJsonUtility.ToJson(catalog);
        bool changed = !string.Equals(before, after, StringComparison.Ordinal);
        if (!changed)
            return false;

        EditorUtility.SetDirty(catalog);
        if (saveAssets)
            AssetDatabase.SaveAssets();
        Debug.Log($"特殊羊目录已同步：{CatalogPath}", catalog);
        return true;
    }

    private static void NormalizeTextureImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out _);
        float pixelsPerUnit = Mathf.Max(1f, sourceWidth);
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool changed = importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit)
            || importer.filterMode != FilterMode.Bilinear
            || importer.mipmapEnabled
            || !importer.alphaIsTransparency
            || importer.wrapMode != TextureWrapMode.Clamp
            || importer.textureCompression != TextureImporterCompression.Compressed
            || settings.spriteGenerateFallbackPhysicsShape;
        if (!changed)
            return;

        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    public static void ScheduleSynchronization()
    {
        if (syncScheduled)
            return;

        syncScheduled = true;
        EditorApplication.delayCall += () =>
        {
            syncScheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleSynchronization();
                return;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Synchronize(LoadOrCreate(), saveAssets: true);
        };
    }

    private static string ResolveStableTypeId(string displayName, string guid)
    {
        switch (displayName?.Trim())
        {
            case "礼帽羊":
            case "礼帽小羊":
                return "sheep.special.tophat";
            case "领结羊":
            case "蝴蝶结羊":
            case "蝴蝶结小羊":
                return "sheep.special.redbow";
            case "山羊":
                return "sheep.special.horned";
            case "黑羊":
            case "黑色小羊":
                return "sheep.special.black";
            default:
                return "sheep.special." + guid.ToLowerInvariant();
        }
    }

    private static bool IsDirectPngChild(string folderPath, string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)
            || !string.Equals(Path.GetExtension(assetPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        return string.Equals(parent, folderPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string CombineAssetPath(string left, string right)
    {
        return (left.TrimEnd('/', '\\') + "/" + right.Trim('/', '\\')).Replace('\\', '/');
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }
}

public sealed class SpecialSheepCatalogAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (ContainsSpecialSheepPath(importedAssets)
            || ContainsSpecialSheepPath(deletedAssets)
            || ContainsSpecialSheepPath(movedAssets)
            || ContainsSpecialSheepPath(movedFromAssetPaths))
        {
            SpecialSheepCatalogEditorUtility.ScheduleSynchronization();
        }
    }

    private static bool ContainsSpecialSheepPath(IEnumerable<string> paths)
    {
        if (paths == null)
            return false;

        string defaultRoot = SpecialSheepCatalog.DefaultSourceRootFolder.TrimEnd('/', '\\') + "/";
        SpecialSheepCatalog catalog = AssetDatabase.LoadAssetAtPath<SpecialSheepCatalog>(
            SpecialSheepCatalogEditorUtility.CatalogPath);
        string configuredRoot = catalog != null
            ? catalog.SourceRootFolder.TrimEnd('/', '\\') + "/"
            : defaultRoot;

        foreach (string path in paths)
        {
            if (path.StartsWith(defaultRoot, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(configuredRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>只更新 Alpha 场景里的特殊羊目录引用，不重建其他场景内容。</summary>
public static class SpecialSheepCatalogSceneBinder
{
    public const string AlphaScenePath = "Assets/_Game/Scenes/AlphaFlockExpansion.unity";

    [MenuItem("Game Jam/Sheep/Bind Catalog To Alpha Scene")]
    public static void BindAlphaScene()
    {
        Scene scene = EditorSceneManager.OpenScene(AlphaScenePath, OpenSceneMode.Single);
        ProgressiveSheepSpawner spawner = UnityEngine.Object.FindAnyObjectByType<ProgressiveSheepSpawner>(
            FindObjectsInactive.Include);
        if (spawner == null)
            throw new InvalidOperationException($"{AlphaScenePath} 中没有 ProgressiveSheepSpawner。");

        SpecialSheepCatalog catalog =
            SpecialSheepCatalogEditorUtility.LoadOrCreateAndSynchronize();
        SerializedObject serialized = new(spawner);
        serialized.FindProperty("specialSheepCatalog").objectReferenceValue = catalog;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"保存场景失败：{AlphaScenePath}");

        AssetDatabase.SaveAssets();
        Debug.Log($"Alpha 场景已绑定特殊羊目录：{SpecialSheepCatalogEditorUtility.CatalogPath}");
    }
}
