using UnityEngine;

public class Roma_Animation : MonoBehaviour
{
    public PlayerControllerTest playerControllerTest;
    private Animator mAnimator;
    

    void Start()
    {
        mAnimator = GetComponent<UnityEngine.Animator>();
    }

   
    void Update()
    {
        if (mAnimator != null)
        {
            bool isMoving = Input.GetKey(KeyCode.W) || 
                            Input.GetKey(KeyCode.A) || 
                            Input.GetKey(KeyCode.S) || 
                            Input.GetKey(KeyCode.D);
            mAnimator.SetBool("Ismove", isMoving);
        }
        else
        {
            mAnimator.SetBool("Ismove", false);
        }
        
    }
}
