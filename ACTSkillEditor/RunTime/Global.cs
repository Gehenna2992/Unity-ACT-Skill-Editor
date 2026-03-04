using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Global
{
    /// <summary>
    /// 跳转条件 (连招窗口)
    /// </summary>
    [System.Serializable]
    public class Jump
    {
        // 跳转的目标技能配置
        public SkillConfigSO nextSkill;

        public int beginKey; // 允许输入的开始帧
        public int endKey;   // 允许输入的结束帧
        // Unity KeyCode
        public KeyCode triggerKey = KeyCode.None;
    }

    /// <summary>
    /// 攻击判定 (增强版)
    /// </summary>
    [System.Serializable]
    public class Attack
    {
        public int keyNumber;       // 关键帧
        public bool isReWrite;      // 是否重写
        public int shapeType;       // 0:球, 1:盒

        // 范围参数
        public float parameter1;    // 半径/长
        public float parameter2;    // 宽
        public Vector3 offset;      // 偏移

        [Header("打击感配置")]
        public float damageRatio = 1.0f;     // 伤害倍率
        public float knockbackForce = 5.0f;  // 击退力度
        public float hitPauseTime = 0.1f;    // 卡肉时间(秒)
        public float cameraShake = 0.5f;     // 震屏力度
        public bool autoAim = true;          // 攻击时是否自动吸附向敌人
    }

    /// <summary>
    /// 特效和音效
    /// </summary>
    [System.Serializable]
    public class FxAndSound
    {
        public int keyNumber;                 // 动画关键帧号
        public ParticleSystem particleSystem; // 特效预制体
        public Vector3 offset;                // 偏移
        public AudioClip audioClip;           // 音效
        public bool followCharacter = true;   // 特效是否跟随角色移动
    }
}