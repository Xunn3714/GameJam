using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SheepNamePool",
    menuName = "Game/Sheep Name Pool")]
public sealed class SheepNamePool : ScriptableObject
{
    [SerializeField] private List<string> adjectives = new();
    [SerializeField] private List<string> nouns = new();

    public IReadOnlyList<string> Adjectives => adjectives;
    public IReadOnlyList<string> Nouns => nouns;

    // Keep the existing Names interface so current systems continue to work.
    public IReadOnlyList<string> Names
    {
        get
        {
            List<string> generatedNames = new();

            foreach (string adjective in adjectives)
            {
                if (string.IsNullOrWhiteSpace(adjective))
                    continue;

                foreach (string noun in nouns)
                {
                    if (string.IsNullOrWhiteSpace(noun))
                        continue;

                    generatedNames.Add(
                        $"{adjective.Trim()}的{noun.Trim()}");
                }
            }

            return generatedNames;
        }
    }
}