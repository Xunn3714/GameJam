using NUnit.Framework;
using UnityEngine;

public sealed class CameraFollow2DTests
{
    [Test]
    public void AdjustOrthographicSize_ClampsToPlayerMinimumAndStageMaximum()
    {
        GameObject cameraObject = CreateCamera(out Camera camera, out CameraFollow2D follow);
        try
        {
            follow.UnlockMaximumOrthographicSize(7f);

            Assert.That(camera.orthographicSize, Is.EqualTo(5f).Within(0.001f));
            Assert.That(follow.TargetOrthographicSize, Is.EqualTo(5f).Within(0.001f));
            Assert.That(follow.AdjustOrthographicSize(1f), Is.True);
            Assert.That(follow.TargetOrthographicSize, Is.EqualTo(4f).Within(0.001f));

            for (int index = 0; index < 10; index++)
                follow.AdjustOrthographicSize(1f);
            Assert.That(follow.TargetOrthographicSize, Is.EqualTo(follow.MinimumOrthographicSize).Within(0.001f));
            Assert.That(follow.AdjustOrthographicSize(1f), Is.False);

            for (int index = 0; index < 10; index++)
                follow.AdjustOrthographicSize(-1f);
            Assert.That(follow.TargetOrthographicSize, Is.EqualTo(7f).Within(0.001f));
            Assert.That(follow.AdjustOrthographicSize(-1f), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void UnlockMaximumOrthographicSize_IsOneWayAndNeverMovesTheCamera()
    {
        GameObject cameraObject = CreateCamera(out Camera camera, out CameraFollow2D follow);
        try
        {
            follow.UnlockMaximumOrthographicSize(7f);
            Assert.That(follow.MaximumOrthographicSize, Is.EqualTo(7f).Within(0.001f));
            Assert.That(follow.TargetOrthographicSize, Is.EqualTo(5f).Within(0.001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(5f).Within(0.001f));

            follow.AdjustOrthographicSize(-1f);
            follow.UnlockMaximumOrthographicSize(10f);
            Assert.That(follow.TargetOrthographicSize, Is.EqualTo(6f).Within(0.001f),
                "Unlocking a new stage must preserve the player's zoom target.");
            Assert.That(follow.MaximumOrthographicSize, Is.EqualTo(10f).Within(0.001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(5f).Within(0.001f));

            follow.UnlockMaximumOrthographicSize(5f);
            Assert.That(follow.MaximumOrthographicSize, Is.EqualTo(10f).Within(0.001f),
                "Unlocked camera stages must never downgrade.");
            Assert.That(follow.TargetOrthographicSize, Is.EqualTo(6f).Within(0.001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    private static GameObject CreateCamera(out Camera camera, out CameraFollow2D follow)
    {
        GameObject cameraObject = new GameObject("CameraFollow2DTests_Camera");
        camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        follow = cameraObject.AddComponent<CameraFollow2D>();
        return cameraObject;
    }
}
