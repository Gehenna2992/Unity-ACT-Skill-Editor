using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class SimpleSkillPlayer : MonoBehaviour
{
    [Header("入口技能")]
    public SkillConfigSO entrySkill1;
    public SkillConfigSO entrySkill2;
    public string defaultLocomotionState = "Idle"; // 你的基础待机状态名[Header("双状态切换 (解决残影Bug)")]

    [Header("双状态切换 (解决残影Bug)")]
    public string skillState1Name = "SkillState1";
    public string skillState2Name = "SkillState2"; [Header("必须拖入！(对应State1和State2里的占位动画)")]
    public AnimationClip placeholderClip1; // <--- 新增
    public AnimationClip placeholderClip2; // <--- 新增
    [Header("移动设置")]
    public float rotationSpeed = 720f;
    public LayerMask hitLayer = ~0;

    // ── 核心组件 ──
    private Animator _animator;
    private CharacterController _cc;
    private Transform _cameraTransform;
    private AnimatorOverrideController _overrideController;

    // 乒乓机制变量
    private string _placeholder1;
    private string _placeholder2;
    private bool _useState1 = false;
    private string _currentActiveStateName;

    // ── 技能与状态 ──
    private SkillConfigSO _currentSkill;
    private AnimationClip _currentClip;
    private bool _isPlaying;
    private int _currentFrame, _lastFrame, _totalFrames;
    private float _frameRate;

    private HashSet<int> _fxTriggeredFrames = new HashSet<int>();
    private HashSet<int> _atkTriggeredFrames = new HashSet<int>();
    private List<GameObject> _activeFxList = new List<GameObject>();

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _cc = GetComponent<CharacterController>();
        _cameraTransform = Camera.main.transform;
        _animator.applyRootMotion = true;

        SetupOverrideController();
    }

    void Update()
    {
        if (!_cc.isGrounded) _cc.Move(Physics.gravity * Time.deltaTime);

        if (_isPlaying && _currentClip != null)
        {
            // ── 乒乓状态读取 ──
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo nextStateInfo = _animator.GetNextAnimatorStateInfo(0);

            // 确定当前是哪个状态在起作用 (处理 CrossFade 的混合延迟)
            bool isCurrent = stateInfo.IsName(_currentActiveStateName);
            bool isNext = nextStateInfo.IsName(_currentActiveStateName);

            if (!isCurrent && !isNext) return; // 还没切过去，等一帧

            AnimatorStateInfo activeInfo = isNext ? nextStateInfo : stateInfo;
            float normalizedTime = activeInfo.normalizedTime;
            float clampedNT = Mathf.Clamp01(normalizedTime);
            _currentFrame = Mathf.FloorToInt(clampedNT * _totalFrames);

            // ★★★ 移动打断检测 ★★★
            if (CheckMoveCancel())
            {
                return; // 如果被移动打断了，直接结束这一帧的技能逻辑
            }

            HandleJumpInput();
            HandleFxAndSound();
            HandleAttackJudge();

            if ((_currentSkill.exitFrame > 0 && _currentFrame >= _currentSkill.exitFrame) || normalizedTime >= 1.0f)
            {
                OnSkillFinish();
            }

            _lastFrame = _currentFrame;
        }
        else
        {
            HandleLocomotion();
        }
    }
    /// <summary>
    /// 移动打断检测
    /// </summary>
    /// <returns></returns>
    bool CheckMoveCancel()
    {
        // 1. 检查当前技能是否允许移动打断 (-1 表示不允许)
        if (_currentSkill.moveCancelFrame < 0) return false;

        // 2. 检查当前帧是否已经到达了允许打断的帧数
        if (_currentFrame >= _currentSkill.moveCancelFrame)
        {
            Debug.Log("true");
            // 3. 检测玩家是否有明显的移动输入
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            // 如果按下了 WASD 任意方向
            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                CancelSkillByMovement();
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// 被移动打断时的清理逻辑
    /// </summary>
    void CancelSkillByMovement()
    {

        _isPlaying = false;
        _currentSkill = null;
        _currentClip = null;

        // 强制 Animator 切回待机/移动状态
        _animator.CrossFade(defaultLocomotionState, 0.05f);

        // 注意：我们不需要在这里写移动代码，因为 _isPlaying 变成 false 后，
        // 下一帧 Update 就会自动进入 HandleLocomotion()，从而流畅地开始行走。
    }
    // ─────────────────────────────────────────────────────────────
    //  技能播放 (乒乓交替)
    // ─────────────────────────────────────────────────────────────
    void PlaySkill(SkillConfigSO skill)
    {
        if (skill == null || skill.skillClip == null) return;
        CleanupFx();

        _currentSkill = skill;
        _currentClip = skill.skillClip;
        _frameRate = _currentClip.frameRate > 0 ? _currentClip.frameRate : 30f;
        _totalFrames = Mathf.Max(1, Mathf.FloorToInt(_currentClip.length * _frameRate));
        _currentFrame = 0; _lastFrame = -1; _isPlaying = true;
        _fxTriggeredFrames.Clear(); _atkTriggeredFrames.Clear();

        // 乒乓切换
        _useState1 = !_useState1;
        _currentActiveStateName = _useState1 ? skillState1Name : skillState2Name;

        // ★★★ 直接使用 Inspector 里拖入的 Clip 作为替换靶子 ★★★
        AnimationClip currentTarget = _useState1 ? placeholderClip1 : placeholderClip2;

        if (currentTarget != null)
        {
            _overrideController[currentTarget] = _currentClip;
        }

        // 强制 Animator 在本帧立刻刷新状态，防止卡帧
        _animator.Update(0f);
        _animator.CrossFade(_currentActiveStateName, 0.1f, 0, 0f);
    }

    void OnSkillFinish()
    {
        _isPlaying = false;

        if (_currentSkill.nextSkillOnFinish != null)
        {
            PlaySkill(_currentSkill.nextSkillOnFinish);
        }
        else
        {
            _animator.CrossFade(defaultLocomotionState, 0.2f);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  初始化与覆盖控制器 (自动寻找两个占位符)
    // ─────────────────────────────────────────────────────────────
    void SetupOverrideController()
    {
        if (_animator.runtimeAnimatorController is AnimatorOverrideController existingOC)
            _overrideController = existingOC;
        else
        {
            _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
            _animator.runtimeAnimatorController = _overrideController;
        }

        // 防呆检测
        if (placeholderClip1 == null || placeholderClip2 == null)
        {
            Debug.LogError("<color=red>[SkillPlayer] 致命错误：请在 Inspector 中拖入 PlaceholderClip 1 和 2！</color>");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  基础模块 (移动、输入、特效、判定) - 保持不变
    // ─────────────────────────────────────────────────────────────
    void HandleLocomotion()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift)) { StartDash(); return; }
        if (Input.GetMouseButtonDown(0) && entrySkill1 != null) { PlaySkill(entrySkill1); return; }
        if (Input.GetKeyDown(KeyCode.E) && entrySkill2 != null) { PlaySkill(entrySkill2); return; }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        
        Vector3 camForward = Vector3.Scale(_cameraTransform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 camRight = _cameraTransform.right;
        Vector3 targetMoveDir = (camRight * h + camForward * v).normalized;

        float inputMagnitude = targetMoveDir.magnitude;
        float currentSpeed = _animator.GetFloat("Speed");

        // ★★★ 修改这里：非对称平滑插值 ★★★
        // 如果玩家在按键 (inputMagnitude > 0)，用平滑的 10f 起步
        // 如果玩家松手 (inputMagnitude == 0)，用极快的 30f 瞬间刹车
        float lerpRate = (inputMagnitude > 0.1f) ? 10f : 30f;

        _animator.SetFloat("Speed", Mathf.Lerp(currentSpeed, inputMagnitude, Time.deltaTime * lerpRate));

        if (inputMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetMoveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            _animator.SetBool("Turning", Vector3.Angle(transform.forward, targetMoveDir) > 120f);
        }
        else
        {
            _animator.SetBool("Turning", false);
        }
    }
    void StartDash()
    {
        float hRaw = Input.GetAxisRaw("Horizontal");
        float vRaw = Input.GetAxisRaw("Vertical");
        if (hRaw == 0 && vRaw == 0) vRaw = 1;
        _animator.SetFloat("DashX", hRaw);
        _animator.SetFloat("DashZ", vRaw);
        StartCoroutine("DashTime");
    }
    IEnumerator DashTime()
    {
        _animator.SetBool("Dash", true);
        yield return new WaitForSeconds(.45f);
        _animator.SetBool("Dash", false);

    }
    void HandleJumpInput()
    {
        if (_currentSkill.jumpList == null) return;
        foreach (var jump in _currentSkill.jumpList)
        {
            if (_currentFrame >= jump.beginKey && _currentFrame <= jump.endKey && Input.GetKeyDown(jump.triggerKey) && jump.nextSkill != null)
            {
                PlaySkill(jump.nextSkill);
                return;
            }
        }
    }

    void HandleFxAndSound()
    {
        if (_currentSkill.fxList == null) return;
        foreach (var fx in _currentSkill.fxList)
        {
            if (fx.keyNumber != _currentFrame || _fxTriggeredFrames.Contains(_currentFrame)) continue;
            _fxTriggeredFrames.Add(_currentFrame);

            if (fx.particleSystem != null)
            {
                ParticleSystem ps = Instantiate(fx.particleSystem, transform.TransformPoint(fx.offset), fx.followCharacter ? transform.rotation : Quaternion.identity);
                if (fx.followCharacter) ps.transform.SetParent(transform);
                ps.Play();
                _activeFxList.Add(ps.gameObject);
                Destroy(ps.gameObject, Mathf.Max(ps.main.duration + ps.main.startLifetime.constantMax, 3f));
            }
            if (fx.audioClip != null) AudioSource.PlayClipAtPoint(fx.audioClip, transform.position);
        }
    }

    void HandleAttackJudge()
    {
        if (_currentSkill.attackList == null) return;
        foreach (var atk in _currentSkill.attackList)
        {
            if (atk.keyNumber != _currentFrame || _atkTriggeredFrames.Contains(_currentFrame)) continue;
            _atkTriggeredFrames.Add(_currentFrame);

            Vector3 worldCenter = transform.TransformPoint(atk.offset);
            Collider[] hits = atk.shapeType == 0 ?
                Physics.OverlapSphere(worldCenter, atk.parameter1, hitLayer) :
                Physics.OverlapBox(worldCenter, new Vector3(atk.parameter1 * 0.5f, 1f, atk.parameter2 * 0.5f), transform.rotation, hitLayer);

            if (hits != null) foreach (var col in hits) if (col.transform != transform && !col.transform.IsChildOf(transform)) Debug.Log($"<color=orange>击中了：{col.gameObject.name}！</color>");
        }
    }

    void CleanupFx()
    {
        foreach (var go in _activeFxList) if (go != null) Destroy(go);
        _activeFxList.Clear();
    }
    void OnDestroy() => CleanupFx();
}