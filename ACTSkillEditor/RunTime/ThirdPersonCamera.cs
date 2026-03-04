using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("追踪目标")]
    [Tooltip("拖入你的角色 (Player)")]
    public Transform target; [Tooltip("摄像机看向的目标偏移 (通常设置在角色胸部/头部，比如 Y=1.5)")]
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

    [Header("距离与视角参数")]
    public float defaultDistance = 4.0f;
    public float minDistance = 1.0f;
    public float maxDistance = 8.0f;
    [Tooltip("鼠标灵敏度")]
    public float mouseSensitivity = 3.0f;
    [Tooltip("滚轮缩放速度")]
    public float scrollSensitivity = 2.0f;

    [Header("视角限制")]
    public float pitchMin = -20f; // 往下看的极限角度
    public float pitchMax = 70f;  // 往上看的极限角度[Header("平滑与防穿模")][Tooltip("摄像机跟随的平滑延迟时间，值越小越紧跟，太大容易晕3D")]
    public float followSmoothTime = 0.05f; [Tooltip("防穿模检测层级 (不要勾选 Player 层，勾选 Default 等环境层)")]
    public LayerMask collisionLayer;
    [Tooltip("摄像机的碰撞体积半径")]
    public float cameraRadius = 0.3f;

    // 内部状态
    private float _yaw;   // 水平旋转角 (Y轴)
    private float _pitch; // 垂直旋转角 (X轴)
    private float _currentDistance;

    private Vector3 _smoothedTargetPosition;
    private Vector3 _currentFollowVelocity;

    void Start()
    {
        // 1. 隐藏并锁定鼠标光标到屏幕中心 (按 ESC 可以释放)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _currentDistance = defaultDistance;

        if (target != null)
        {
            _smoothedTargetPosition = target.position + targetOffset;
            // 继承摄像机初始的旋转角
            Vector3 angles = transform.eulerAngles;
            _pitch = angles.x;
            _yaw = angles.y;
        }
    }

    // 摄像机逻辑必须放在 LateUpdate，确保在角色动画和移动完全结算后执行
    void LateUpdate()
    {
        if (target == null) return;

        // ── 1. 处理鼠标输入 ──
        _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 限制上下看角度，防止翻转
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        // 处理滚轮缩放
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _currentDistance -= scroll * scrollSensitivity;
            _currentDistance = Mathf.Clamp(_currentDistance, minDistance, maxDistance);
        }

        // ── 2. 平滑跟随目标点 ──
        // 这一步非常重要：消除角色RootMotion走路时的微小抖动，让画面更稳
        Vector3 desiredTargetPos = target.position + targetOffset;
        _smoothedTargetPosition = Vector3.SmoothDamp(
            _smoothedTargetPosition,
            desiredTargetPos,
            ref _currentFollowVelocity,
            followSmoothTime
        );

        // ── 3. 计算旋转与理论位置 ──
        Quaternion currentRotation = Quaternion.Euler(_pitch, _yaw, 0);
        Vector3 desiredCameraPos = _smoothedTargetPosition - (currentRotation * Vector3.forward * _currentDistance);

        // ── 4. 物理防穿模检测 (SphereCast) ──
        // 从目标点向摄像机的方向发射一个球形射线，检查是否撞墙
        float actualDistance = _currentDistance;
        Vector3 directionToCamera = desiredCameraPos - _smoothedTargetPosition;

        if (Physics.SphereCast(_smoothedTargetPosition, cameraRadius, directionToCamera.normalized, out RaycastHit hit, _currentDistance, collisionLayer))
        {
            // 如果撞到了墙壁，就把实际距离缩短到撞击点的位置
            actualDistance = hit.distance;
        }

        // ── 5. 应用最终的 Transform ──
        transform.position = _smoothedTargetPosition - (currentRotation * Vector3.forward * actualDistance);
        transform.rotation = currentRotation;
    }
}