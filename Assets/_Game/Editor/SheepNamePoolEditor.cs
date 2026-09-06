using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SheepNamePool))]
public class SheepNamePoolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SheepNamePool pool = (SheepNamePool)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Fill Default Name Pool"))
        {
            SerializedObject serializedPool = new SerializedObject(pool);

            SerializedProperty adjectives =
                serializedPool.FindProperty("adjectives");

            SerializedProperty nouns =
                serializedPool.FindProperty("nouns");

            string[] adjectiveValues =
            {
                "疯狂","窒息","中二","邪恶","神秘","暴躁","冷酷","迷茫","阴险","可疑",
                "诡异","狂野","失控","黑化","暴走","觉醒","封印","终焉","深渊","禁忌",
                "混沌","虚无","末日","灭世","逆天","神圣","堕落","无敌","绝望","疯癫",
                "慌张","阴郁","孤独","高冷","傲娇","呆滞","笨拙","优雅","滑稽","诙谐",
                "古怪","奇妙","荒唐","离谱","抽象","魔性","诡谲","神经","沉默","喧嚣",
                "炽热","冰冷","灼热","阴冷","漆黑","闪耀","发光","透明","巨大","微小",
                "肥硕","苗条","圆润","尖锐","柔软","坚硬","黏糊","湿润","干瘪","蓬松",
                "毛躁","香甜","苦涩","酸爽","腥臭","清香","腐朽","新鲜","过期","破碎",
                "完整","孤高","贪婪","慵懒","勤奋","紧张","淡定","愤怒","开心","悲伤",
                "兴奋","困倦","饥饿","饱胀","聪明","愚蠢","狡猾","忠诚","叛逆","佛系"
            };

            string[] nounValues =
            {
                "橘子","火车","拖鞋","冰箱","土豆","电扇","牙刷","锅盖","路灯","水桶",
                "面包","雨伞","纸箱","螺丝","枕头","叉子","窗帘","闹钟","石榴","垃圾",
                "水壶","扫把","马桶","纸巾","衣架","键盘","鼠标","插座","井盖","车票",
                "手册","快递","西瓜","香蕉","苹果","葡萄","柠檬","草莓","桃子","梨子",
                "榴莲","芒果","玉米","茄子","白菜","萝卜","洋葱","大蒜","辣椒","豆腐",
                "馒头","包子","饺子","面条","米饭","饼干","蛋糕","牛奶","可乐","咖啡",
                "奶茶","棉花","泡面","火锅","铁锅","勺子","筷子","盘子","杯子","瓶子",
                "脸盆","毛巾","肥皂","镜子","梳子","袜子","裤子","帽子","围巾","手套",
                "书包","铅笔","橡皮","尺子","本子","书桌","椅子","沙发","床垫","门锁",
                "钥匙","电池","灯泡","电线","插头","喇叭","音箱","相机","手机","电脑"
            };

            FillArray(adjectives, adjectiveValues);
            FillArray(nouns, nounValues);

            serializedPool.ApplyModifiedProperties();
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();
        }
    }

    private static void FillArray(
        SerializedProperty property,
        string[] values)
    {
        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}