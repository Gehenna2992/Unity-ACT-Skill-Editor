using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSkillConfig", menuName = "Combat/Skill Config")]
public class SkillConfigSO : ScriptableObject
{
    [Header("基本信息")]
    public int skillID;             // 技能ID
    public string skillName;        // 技能名称
    [TextArea] public string skillDescription; // 技能描述
    public int skillType;           // 技能类型 (0:普攻, 1:技能, 2:被动 等)
    public string previewModelName; // 仅用于编辑器预览的名字记录

    [Header("技能参数")]
    public float skillCD;           // 技能CD
    // public int resourceValue;    // 资源消耗值 

    [Header("动作表现")]
    public AnimationClip skillClip; // 核心动画片段
    public int exitFrame = 0;//0表示播放完才推出，》0表示到达该帧就退出

    // 播完后自动衔接的技能 (例如: 收刀动作配置)。默认为空(即回Idle)
    public SkillConfigSO nextSkillOnFinish;

    [Header("位移控制")]
    // ZZZ核心：使用曲线控制每一帧的移动速度
    public AnimationCurve moveCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));
    public float totalMoveDistance = 0f; // 配合曲线的总突进距离

    [Header("核心数据列表")]
    // 核心数据：跳转、攻击判定、特效
    public List<Global.Jump> jumpList = new List<Global.Jump>();
    public List<Global.Attack> attackList = new List<Global.Attack>();
    public List<Global.FxAndSound> fxList = new List<Global.FxAndSound>();
    [Header("打断机制")]
    [Tooltip("从第几帧开始允许被移动(WASD)打断？填 0 表示随时可打断，填 -1 表示不可被打断")]
    public int moveCancelFrame = -1;

}