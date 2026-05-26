using UnityEngine;

public partial class CustomerAgent
{
    private void PlayAnim(string animName)
    {
        if (animator == null || currentAnim == animName)
            return;

        animator.Play(animName);
        currentAnim = animName;
    }

    private void PlayWalkAnimation(Vector3 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            PlayAnim(direction.x > 0 ? "walk_right" : "walk_left");
        }
        else
        {
            PlayAnim(direction.y > 0 ? "walk_back" : "walk_front");
        }
    }

    private void SetIdleFromLastDirection()
    {
        if (currentAnim == "walk_right")
            PlayAnim("idle_right");
        else if (currentAnim == "walk_left")
            PlayAnim("idle_left");
        else if (currentAnim == "walk_back")
            PlayAnim("idle_back");
        else if (currentAnim == "walk_front")
            PlayAnim("idle_front");
    }

    private void FaceCell(GridCell cell)
    {
        if (cell == null)
            return;

        Vector2Int dir = cell.Pos - currentPos;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            PlayAnim(dir.x > 0 ? "idle_right" : "idle_left");
        }
        else
        {
            PlayAnim(dir.y > 0 ? "idle_back" : "idle_front");
        }
    }
}
