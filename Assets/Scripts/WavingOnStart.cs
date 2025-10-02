using UnityEngine;

public class WavingOnStart : MonoBehaviour
{
    private Animator animator;
    string s = "Waving";

    private void Start()
    {
        animator = GetComponent<Animator>();

        if (animator == null )
        {
            Debug.LogError("Nenhum animador");
        }

        animator.SetTrigger(s);
    }
}
