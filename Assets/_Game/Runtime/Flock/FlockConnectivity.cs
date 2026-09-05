using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>以指定成员为锚点，找出可通过近邻关系抵达的主羊群。</summary>
public static class FlockConnectivity
{
    public static void CollectConnectedIndices(
        IReadOnlyList<Vector2> positions,
        int anchorIndex,
        float maximumLinkDistance,
        Func<int, int, bool> canConnect,
        HashSet<int> connectedIndices,
        Queue<int> traversal)
    {
        if (connectedIndices == null)
            throw new ArgumentNullException(nameof(connectedIndices));
        if (traversal == null)
            throw new ArgumentNullException(nameof(traversal));

        connectedIndices.Clear();
        traversal.Clear();
        if (positions == null || anchorIndex < 0 || anchorIndex >= positions.Count)
            return;

        float linkDistanceSquared = Mathf.Max(0f, maximumLinkDistance)
            * Mathf.Max(0f, maximumLinkDistance);
        connectedIndices.Add(anchorIndex);
        traversal.Enqueue(anchorIndex);

        while (traversal.Count > 0)
        {
            int currentIndex = traversal.Dequeue();
            for (int candidateIndex = 0; candidateIndex < positions.Count; candidateIndex++)
            {
                if (connectedIndices.Contains(candidateIndex))
                    continue;
                if ((positions[candidateIndex] - positions[currentIndex]).sqrMagnitude > linkDistanceSquared)
                    continue;
                if (canConnect != null && !canConnect(currentIndex, candidateIndex))
                    continue;

                connectedIndices.Add(candidateIndex);
                traversal.Enqueue(candidateIndex);
            }
        }
    }
}
