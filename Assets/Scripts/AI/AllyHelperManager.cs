using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(100)]
public class AllyHelperManager : MonoBehaviour
{
   [SerializeField] private PlayerContext playerContext;
   [SerializeField] private AllyContext allyContext;
   [SerializeField] private GameObject allyHelper;
   private CharacterAnimBrain allyAnimBrain;
   private bool hideHelperOnSkillComplete;
   

       
   [Header("Summon")]
   [SerializeField] private float summonRadius = 2.5f;
   [SerializeField] private float minSummonRadius = 1.2f;
   [SerializeField] private float navMeshSampleDistance = 2f;
   [SerializeField] private bool facePlayerForward = true;
    
    void Start()
    {
        if (allyHelper == null)
        {
            Debug.LogWarning("AllyHelper is null", this);
            return;
        }

        allyContext = allyHelper.GetComponent<AllyContext>();
        allyAnimBrain = allyContext != null ? allyContext.AnimBrain : null;

        if (allyAnimBrain == null)
            allyAnimBrain = allyHelper.GetComponent<CharacterAnimBrain>();

        if (allyContext != null && allyContext.AnimBrain == null)
            allyContext.AnimBrain = allyAnimBrain;

        if (allyAnimBrain != null)
            allyAnimBrain.SkillCompleted += OnAllySkillCompleted;

        allyHelper.SetActive(false);
    }

    private void OnDestroy()
    {
        if (allyAnimBrain != null)
            allyAnimBrain.SkillCompleted -= OnAllySkillCompleted;
    }

    
    public void SummonAllyHelper()
    {
        if (playerContext == null || allyHelper == null)
        {
            Debug.LogWarning("Summon failed : playerContext or allyHelper is null", this);
            return;
        }

        Vector3 playerPos = playerContext.transform.position;

        // สุ่มตำแหน่งรอบตัว player บนระนาบ XZ
        Vector2 random2D = Random.insideUnitCircle.normalized * Random.Range(minSummonRadius, summonRadius);
        Vector3 rawSpawnPos = playerPos + new Vector3(random2D.x, 0f, random2D.y);

        Vector3 finalSpawnPos = rawSpawnPos;

        // ถ้ามี NavMesh ให้แปะลง NavMesh ก่อน
        if (NavMesh.SamplePosition(rawSpawnPos, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            finalSpawnPos = hit.position;
        }
        else
        {
            // fallback ถ้าหา NavMesh ไม่เจอ
            finalSpawnPos = rawSpawnPos;
        }

        // ตั้งตำแหน่ง
        allyHelper.transform.position = finalSpawnPos;

        // ตั้งทิศ
        if (facePlayerForward)
        {
            allyHelper.transform.rotation = playerContext.transform.rotation;
        }
        else
        {
            Vector3 lookDir = (playerPos - finalSpawnPos);
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.001f)
                allyHelper.transform.rotation = Quaternion.LookRotation(lookDir);
        }

        // เปิดใช้งาน
        if (allyAnimBrain == null)
        {
            Debug.LogWarning("Summon failed : CharacterAnimBrain is null", this);
            return;
        }

        if (!allyHelper.activeSelf)
            allyHelper.SetActive(true);

        hideHelperOnSkillComplete = true;
        allyAnimBrain.PlaySkill();

        if (!allyAnimBrain.IsSkillActive)
            hideHelperOnSkillComplete = false;
        
    }

    public void AllyHelperOut()
    {
        hideHelperOnSkillComplete = false;

        if (allyHelper != null && allyHelper.activeSelf)
            allyHelper.SetActive(false);
    }

    private void OnAllySkillCompleted()
    {
        if (!hideHelperOnSkillComplete)
            return;

        AllyHelperOut();
    }
}
