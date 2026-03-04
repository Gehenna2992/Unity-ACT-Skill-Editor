using AtkJudge;
using Codice.CM.Client.Differences.Graphic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class SkillEditorWindow : EditorWindow
{
    // --- 临时编辑变量 ---
    private int skillID;
    public string skillName;
    public string skillDescription;
    public int skillTypeSelectIndex;
    private string[] skillTypeArray = new string[] { "普攻", "主动技能", "被动技能" };
    private GameObject previewFxInstance;//特效
    private int lastFxFrame = -1; // 记录上一次播放特效的帧，防止重复生成
    private double m_LastEditorTime;

    public float skillCD;

    // 判定框编辑辅助变量
    public int skillShapeSelectIndex; // 0:球, 1:盒
    private string[] skillShapeArray = new string[] { "圆形", "矩形" };
    public float range1;// 编辑时的临时参数1
    public float range2;// 编辑时的临时参数2
    public float offsetX, offsetY, offsetZ; // 编辑时的临时偏移

    // 状态折叠
    bool m_Foldout1 = true;
    GUIContent m_Content1 = new GUIContent("基本信息");
    bool m_Foldout2;
    GUIContent m_Content2 = new GUIContent("位移信息");
    bool m_Foldout3;
    GUIContent m_Content3 = new GUIContent("判定框默认参数 (非重写帧使用)");

    // 文件路径相关
    string filePath;
    public SkillConfigSO configFile; // 当前编辑的配置文件

    // --- 模型与动画预览变量 ---
    public GameObject previewModel; // 场景预览模型 (Idle/T-Pose)
    public GameObject animSourceModel; // 动作来源模型 (FBX资源)
    private Animator previewAnim; // 预览模型的Animator
    private AnimationClip clip; // 当前选中的Clip

    // 动画播放控制
    private float keyNow; // 当前播放到的时间点
    private int frameSelectIndex; // 当前选择的帧索引
    private int last_frameSelectIndex;
    private bool playFrame = false; // 是否正在播放
    private float playTimer;
    private float frameRate = 0.033f;// 默认30fps
    private Vector2 scrollView = new Vector2(0, 0); // 帧条的滚动位置
    private Vector2 mainScrollPos;// 主窗口滚动条

    // --- 核心数据列表  ---
    private List<Global.Jump> jumpList = new List<Global.Jump>();
    private List<Global.Attack> atkList = new List<Global.Attack>();
    private List<Global.FxAndSound> fxAndSoundList = new List<Global.FxAndSound>();

    // --- 自动衔接 ---
    public SkillConfigSO _nextSkillOnFinish;

    // --- 场景绘制辅助 ---
    bool isDrawAtk = false;// 是否绘制攻击范围
    bool isRewriteAtk = false; // 当前帧是否重写了范围

    // 判定框绘制工具
    AtkJudge.IJudgeArea _IJudgeArea = null;
    Judgment _judgment = new Judgment();
    BoxBoundsHandle boxHandle = new BoxBoundsHandle();
    SphereBoundsHandle sphereHandle = new SphereBoundsHandle();
    AtkJudge.BoxItem boxItem = new AtkJudge.BoxItem();
    AtkJudge.SphereItem sphereItem = new AtkJudge.SphereItem();

    // 特效预览
    public ParticleSystem particleSystem;
    float fxDuration;
    double fxLastTime;
    float fxDurationMax = 5.0f;
    public AudioSource as1;

    // 缓存提取出来的动画
    private AnimationClip[] currentClips;

    [MenuItem("工具/技能编辑器")]
    static void Open()
    {
        SkillEditorWindow window = GetWindow<SkillEditorWindow>();
        window.Show();
    }

    private void OnEnable()
    {
        if (Camera.main != null)
            as1 = Camera.main.GetComponent<AudioSource>();
        SceneView.duringSceneGui += OnSceneGUI;
        m_LastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += OnEditorUpdate;
    }
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= OnEditorUpdate;
        CleanPreviewFx(); // 关闭窗口时清理垃圾
    }
    void OnEditorUpdate()
    {
        double currentTime = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(currentTime - m_LastEditorTime);
        m_LastEditorTime = currentTime;

        // 现在使用这个可靠的deltaTime来更新你的动画和特效
        AnimPlayUpdate(deltaTime);

        // FxUpdate 也需要调整，直接使用deltaTime
        FxUpdate(deltaTime);
    }
    void AnimPlayUpdate(float deltaTime)
    {
        if (!playFrame || clip == null) return;
        if (clip.frameRate > 0) frameRate = 1.0f / clip.frameRate;
        int maxFrame = (int)(clip.length * clip.frameRate);
        if (maxFrame <= 0) return;

        playTimer += deltaTime; // 直接使用传入的deltaTime
        while (playTimer > frameRate)
        {
            playTimer -= frameRate;
            frameSelectIndex++;
            if (frameSelectIndex >= maxFrame)
            {
                frameSelectIndex = 0;
            }
        }
        Repaint(); // 请求重绘窗口来更新进度条
    }
    void FxUpdate(float deltaTime)
    {
        if (Application.isPlaying || previewFxInstance == null) return;

        fxDuration += deltaTime; // 直接使用传入的deltaTime

        var parts = previewFxInstance.GetComponentsInChildren<ParticleSystem>();
        foreach (var p in parts)
        {
            p.Simulate(fxDuration, true, false);
        }
    }
    #region 场景绘制与更新

    void OnSceneGUI(SceneView sceneView)
    {
        OnSceneGUI();// 绘制Handle

        sceneView.Repaint();
        Repaint();
    }

    

    void OnSceneGUI()
    {
        // 如果当前帧没有判定，且也没勾选绘制，就不画
        if (!isDrawAtk && !isRewriteAtk) return;

        // 确定当前绘制的数据来源（是默认值还是当前帧的重写值）
        if (!isRewriteAtk)
        {
            // 使用面板上的临时变量
            switch (skillShapeSelectIndex)
            {
                case 1: // Box
                    _IJudgeArea = boxItem;
                    _IJudgeArea.SetValue(range1, 1, range2, offsetX, offsetY, offsetZ);
                    break;
                case 0: // Sphere
                    _IJudgeArea = sphereItem;
                    _IJudgeArea.SetValue(range1, 0, 0, offsetX, offsetY, offsetZ);
                    break;
            }
        }
        else
        {
            // 使用列表里的数据
            int index = FindKeyIndex(frameSelectIndex);
            if (index >= 0)
            {
                var atk = atkList[index];
                switch (atk.shapeType)
                {
                    case 1:
                        _IJudgeArea = boxItem;
                        _IJudgeArea.SetValue(atk.parameter1, 1, atk.parameter2, atk.offset.x, atk.offset.y, atk.offset.z);
                        break;
                    case 0:
                        _IJudgeArea = sphereItem;
                        _IJudgeArea.SetValue(atk.parameter1, 0, 0, atk.offset.x, atk.offset.y, atk.offset.z);
                        break;
                }
            }
        }

        _judgment.value = _IJudgeArea;

        
        if (previewModel != null)
        {
            Matrix4x4 localToWorld = previewModel.transform.localToWorldMatrix;
            DrawJudjeArea(_judgment, localToWorld, new Color(1, 0, 0, 0.25f));
        }
    }

    void DrawJudjeArea(Judgment judgment, Matrix4x4 localToWorld, Color color)
    {
        // 转换矩阵到模型空间 (保持缩放和旋转)
        Matrix4x4 temp = Matrix4x4.TRS(localToWorld.MultiplyPoint3x4(Vector3.zero), localToWorld.rotation, localToWorld.lossyScale);//Vector3.one


        Handles.matrix = temp;

        DrawRange(_judgment, color);
        DrawHandle(_judgment.value);
    }

    void DrawRange(Judgment config, Color color)
    {
        HandlesDrawTool.H.PushColor(color);
        HandlesDrawTool.H.isFill = true;
        switch (config.value)
        {
            case AtkJudge.BoxItem v:
                HandlesDrawTool.H.DrawBox(v.size, Matrix4x4.Translate(v.offset));
                break;
            case AtkJudge.SphereItem v:
                HandlesDrawTool.H.DrawSphere(v.radius, Matrix4x4.Translate(v.offset));
                break;
        }
        HandlesDrawTool.H.isFill = false;
        HandlesDrawTool.H.PopColor();
    }

    /// <summary>
    /// 绘制可交互的手柄 
    /// </summary>
    /// <param name="config"></param>
    void DrawHandle(AtkJudge.IJudgeArea config)
    {
        Vector3 offset = Vector3.zero;
        Vector3 size = Vector3.one;
        switch (config)
        {
            case AtkJudge.BoxItem v:
                offset = v.offset;
                size = v.size;
                break;
            case AtkJudge.SphereItem v:
                offset = v.offset;
                size = new Vector2(v.radius, 0);
                break;
        }

        float handlerSize = HandleUtility.GetHandleSize(offset);

        // 只有在Move/Scale/Rect工具下才显示Handle，避免干扰视图
        switch (Tools.current)
        {
            case Tool.Move:
                offset = Handles.DoPositionHandle(offset, Quaternion.identity);
                break;
            case Tool.Scale:
                size = Handles.DoScaleHandle(size, offset, Quaternion.identity, handlerSize);
                break;
            case Tool.Rect:
                switch (config)
                {
                    case AtkJudge.BoxItem v:
                        boxHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z; // 只调整XZ平面
                        boxHandle.center = offset;
                        boxHandle.size = size;
                        boxHandle.DrawHandle();
                        offset = boxHandle.center;
                        size = boxHandle.size;
                        break;
                    case AtkJudge.SphereItem v:
                        sphereHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y | PrimitiveBoundsHandle.Axes.Z;
                        sphereHandle.center = offset;
                        sphereHandle.radius = size.x;
                        sphereHandle.DrawHandle();
                        offset = sphereHandle.center;
                        size.x = sphereHandle.radius;
                        break;
                }
                break;
        }

        // 回写数据
        switch (config)
        {
            case AtkJudge.BoxItem v:
                v.offset = offset;
                v.size = size;
                if (!isRewriteAtk)
                {
                    offsetX = v.offset.x; offsetY = v.offset.y; offsetZ = v.offset.z;
                    range1 = v.size.x; range2 = v.size.z;
                }
                else
                {
                    UpdateAtkListFromScene(v.size.x, v.size.z, v.offset);
                }
                break;
            case AtkJudge.SphereItem v:
                v.offset = offset;
                v.radius = size.x;
                if (!isRewriteAtk)
                {
                    offsetX = v.offset.x; offsetY = v.offset.y; offsetZ = v.offset.z;
                    range1 = v.radius;
                }
                else
                {
                    UpdateAtkListFromScene(v.radius, 0, v.offset);
                }
                break;
        }
    }

    void UpdateAtkListFromScene(float p1, float p2, Vector3 off)
    {
        int index = FindKeyIndex(frameSelectIndex);
        if (index >= 0)
        {
            atkList[index].offset = off;
            atkList[index].parameter1 = p1;
            atkList[index].parameter2 = p2;
        }
    }

    #endregion

    #region 编辑器界面

    private void OnGUI()
    {
        mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);
        EditorGUI.indentLevel += 1;

        // --- 顶部：文件操作 ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("新建配置", GUILayout.Height(30)))
        {
            CreateNewConfig();
        }
        if (GUILayout.Button("保存配置", GUILayout.Height(30)))
        {
            SaveConfig();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        configFile = EditorGUILayout.ObjectField("当前配置文件", configFile, typeof(SkillConfigSO), false) as SkillConfigSO;
        if (EditorGUI.EndChangeCheck() && configFile != null)
        {
            LoadConfig();
        }

        if (configFile == null)
        {
            EditorGUILayout.HelpBox("请新建或选择一个配置文件开始编辑", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        // --- 1. 基本信息 ---
        EditorGUILayout.BeginVertical(GUI.skin.box);
        m_Foldout1 = EditorGUILayout.Foldout(m_Foldout1, m_Content1);
        if (m_Foldout1)
        {
            skillID = EditorGUILayout.IntField("技能ID:", skillID);
            skillName = EditorGUILayout.TextField("技能名称:", skillName);
            EditorGUILayout.LabelField("技能描述:");
            skillDescription = EditorGUILayout.TextArea(skillDescription, GUILayout.Height(40));
            skillTypeSelectIndex = EditorGUILayout.Popup("技能类型:", skillTypeSelectIndex, skillTypeArray);
            skillCD = EditorGUILayout.FloatField("技能CD:", skillCD);
        }
        EditorGUILayout.EndVertical();

        // --- 2. 位移曲线 ---
        EditorGUILayout.BeginVertical(GUI.skin.box);
        m_Foldout2 = EditorGUILayout.Foldout(m_Foldout2, m_Content2);
        if (m_Foldout2)
        {
            if (configFile.moveCurve == null) configFile.moveCurve = new AnimationCurve();
            configFile.moveCurve = EditorGUILayout.CurveField("速度/位移曲线", configFile.moveCurve);
            configFile.totalMoveDistance = EditorGUILayout.FloatField("总突进距离", configFile.totalMoveDistance);
        }
        EditorGUILayout.EndVertical();

        // --- 3. 模型与动画预览 ---
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("模型与动画源", EditorStyles.boldLabel);

        // 3.1 预览模型 (Idle)
        EditorGUI.BeginChangeCheck();
        previewModel = EditorGUILayout.ObjectField("场景预览模型", previewModel, typeof(GameObject), false) as GameObject;
        if (EditorGUI.EndChangeCheck() && previewModel != null)
        {
            previewAnim = previewModel.GetComponent<Animator>();
        }

        // 3.2 动作源 (FBX)
        EditorGUI.BeginChangeCheck();
        animSourceModel = EditorGUILayout.ObjectField("动作来源模型", animSourceModel, typeof(GameObject), false) as GameObject;
        if (EditorGUI.EndChangeCheck())
        {
            LoadClipsFromAsset();
            // 3.3 动画片段选择
            if (currentClips != null && currentClips.Length > 0)
            {
                clip = currentClips[0];
            }
            else
            {
                EditorGUILayout.HelpBox("请拖入包含动画的模型文件", MessageType.Info);
            }
        }

        

        clip = EditorGUILayout.ObjectField("当前技能动画", clip, typeof(AnimationClip), false) as AnimationClip;

        // 3.4 自动衔接配置
        _nextSkillOnFinish = EditorGUILayout.ObjectField("结束后自动衔接", _nextSkillOnFinish, typeof(SkillConfigSO), false) as SkillConfigSO;
        if (_nextSkillOnFinish == null)
            EditorGUILayout.HelpBox("为空时，技能结束后自动返回 Idle。", MessageType.None);

        EditorGUILayout.EndVertical();

        // --- 4. 动画时间轴 & 预览 ---
        if (previewModel != null && clip != null)
        {
            // 强制采样动画到预览模型
            clip.SampleAnimation(previewModel, keyNow);

            // 滑动条
            int totalFrames = (int)(clip.length * clip.frameRate);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("帧进度:", GUILayout.Width(50),GUILayout.Width(80));
            frameSelectIndex = EditorGUILayout.IntSlider(frameSelectIndex, 0, totalFrames);
            EditorGUILayout.LabelField($" / {totalFrames} ({(float)frameSelectIndex / clip.frameRate:F2}s)");
            EditorGUILayout.EndHorizontal();

            if (last_frameSelectIndex != frameSelectIndex)
            {
                PlayFxAndSound(frameSelectIndex);
            }
            last_frameSelectIndex = frameSelectIndex;

            // 播放按钮
            if (GUILayout.Button(playFrame ? "停止预览" : "播放预览", GUILayout.Height(25)))
            {
                playTimer = 0;
                playFrame = !playFrame;
            }

            // 绘制帧序列按钮
            DrawFrames(clip);
        }

        // --- 5. 判定框配置 (默认值) ---
        EditorGUILayout.BeginVertical(GUI.skin.box);
        m_Foldout3 = EditorGUILayout.Foldout(m_Foldout3, m_Content3);
        if (m_Foldout3)
        {
            skillShapeSelectIndex = EditorGUILayout.Popup("形状:", skillShapeSelectIndex, skillShapeArray);
            range1 = EditorGUILayout.FloatField("参数1 (半径/长):", range1);
            range2 = EditorGUILayout.FloatField("参数2 (宽):", range2);
            Vector3 tempOff = new Vector3(offsetX, offsetY, offsetZ);
            tempOff = EditorGUILayout.Vector3Field("偏移:", tempOff);
            offsetX = tempOff.x; offsetY = tempOff.y; offsetZ = tempOff.z;
        }
        EditorGUILayout.EndVertical();

        // --- 6. 跳转窗口配置 ---
        DrawJumpConfig();

        // --- 7. 攻击判定列表 ---
        DrawAttackConfig();

        // --- 8. 特效音效列表 ---
        DrawFxConfig();

        EditorGUILayout.EndScrollView();
    }

    // --- 绘制跳转配置 ---
    void DrawJumpConfig()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField($"连招跳转窗口 ({jumpList.Count})", EditorStyles.boldLabel);

        if (GUILayout.Button("+ 添加跳转窗口"))
        {
            Global.Jump j = new Global.Jump();
            j.beginKey = frameSelectIndex;
            j.endKey = frameSelectIndex + 10;
            jumpList.Add(j);
        }

        for (int i = 0; i < jumpList.Count; i++)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();

            // 目标
            EditorGUIUtility.labelWidth = 70;
            jumpList[i].nextSkill = EditorGUILayout.ObjectField("目标技能", jumpList[i].nextSkill, typeof(SkillConfigSO), false) as SkillConfigSO;

            // 按键 (标准 KeyCode)
            EditorGUIUtility.labelWidth = 70;
            jumpList[i].triggerKey = (KeyCode)EditorGUILayout.EnumPopup("触发按键", jumpList[i].triggerKey);

            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                jumpList.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            // 帧范围
            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 65;
            jumpList[i].beginKey = EditorGUILayout.IntField("开始帧", jumpList[i].beginKey);
            jumpList[i].endKey = EditorGUILayout.IntField("结束帧", jumpList[i].endKey);


            // 范围校验
            if (clip != null)
            {
                int max = (int)(clip.length * clip.frameRate);
                jumpList[i].beginKey = Mathf.Clamp(jumpList[i].beginKey, 0, max);
                jumpList[i].endKey = Mathf.Clamp(jumpList[i].endKey, jumpList[i].beginKey, max);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制攻击判定列表
    /// </summary>
    void DrawAttackConfig()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField($"攻击判定帧 ({atkList.Count})", EditorStyles.boldLabel);

        if (GUILayout.Button("+ 在当前帧添加判定"))
        {
            Global.Attack atk = new Global.Attack();
            atk.keyNumber = frameSelectIndex;
            // 默认继承当前的形状设置
            atk.shapeType = skillShapeSelectIndex;
            atk.parameter1 = range1;
            atk.parameter2 = range2;
            atk.offset = new Vector3(offsetX, offsetY, offsetZ);
            atk.isReWrite = true; // 默认重写，方便在场景调节
            atkList.Add(atk);
        }

        for (int i = 0; i < atkList.Count; i++)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 70;
            EditorGUILayout.LabelField($"帧: {atkList[i].keyNumber}", GUILayout.Width(60));
            atkList[i].isReWrite = EditorGUILayout.Toggle("独立编辑", atkList[i].isReWrite,GUILayout.Width(90));

            if (GUILayout.Button("Go", GUILayout.Width(30))) frameSelectIndex = atkList[i].keyNumber;
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                atkList.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (atkList[i].isReWrite)
            {
                EditorGUI.indentLevel++;
                atkList[i].shapeType = EditorGUILayout.Popup("形状", atkList[i].shapeType, skillShapeArray);
                EditorGUIUtility.labelWidth =80;
                // 打击感参数
                atkList[i].knockbackForce = EditorGUILayout.FloatField("击退力度", atkList[i].knockbackForce);
                atkList[i].hitPauseTime = EditorGUILayout.FloatField("卡肉时间", atkList[i].hitPauseTime);
                atkList[i].cameraShake = EditorGUILayout.FloatField("震屏力度", atkList[i].cameraShake);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制特效列表
    /// </summary>
    void DrawFxConfig()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField($"特效与音效 ({fxAndSoundList.Count})", EditorStyles.boldLabel);

        if (GUILayout.Button("+ 在当前帧添加特效"))
        {
            Global.FxAndSound fx = new Global.FxAndSound();
            fx.keyNumber = frameSelectIndex;
            fxAndSoundList.Add(fx);
        }

        for (int i = 0; i < fxAndSoundList.Count; i++)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            fxAndSoundList[i].keyNumber = EditorGUILayout.IntField("帧:", fxAndSoundList[i].keyNumber, GUILayout.Width(100));

            if (GUILayout.Button("Go", GUILayout.Width(30))) frameSelectIndex = fxAndSoundList[i].keyNumber;
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                fxAndSoundList.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUIUtility.labelWidth = 100;
            fxAndSoundList[i].particleSystem = EditorGUILayout.ObjectField("特效Prefab", fxAndSoundList[i].particleSystem, typeof(ParticleSystem), false) as ParticleSystem;
            fxAndSoundList[i].audioClip = EditorGUILayout.ObjectField("音效Clip", fxAndSoundList[i].audioClip, typeof(AudioClip), false) as AudioClip;
            fxAndSoundList[i].offset = EditorGUILayout.Vector3Field("偏移", fxAndSoundList[i].offset);
            fxAndSoundList[i].followCharacter = EditorGUILayout.Toggle("跟随角色", fxAndSoundList[i].followCharacter);

            // 预览当前特效位置 
            if (particleSystem != null && previewModel != null && frameSelectIndex == fxAndSoundList[i].keyNumber)
            {
                particleSystem.transform.position = previewModel.transform.position + fxAndSoundList[i].offset;
            }

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();
    }

    #endregion

    #region 辅助方法

    void CreateNewConfig()
    {
        int fileIndex = 0;
        while (true)
        {
            filePath = "Assets/Resources/SkillConfigs/Skill_" + fileIndex + ".asset";
            if (File.Exists(filePath))
            {
                ++fileIndex;
                continue;
            }
            break;
        }
        Directory.CreateDirectory("Assets/Resources/SkillConfigs");

        SkillConfigSO asset = ScriptableObject.CreateInstance<SkillConfigSO>();
        AssetDatabase.CreateAsset(asset, filePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        configFile = asset;
        LoadConfig();
    }

    void LoadConfig()
    {
        if (configFile == null) return;

        // 基础信息
        skillID = configFile.skillID;
        skillName = configFile.skillName;
        skillDescription = configFile.skillDescription;
        skillTypeSelectIndex = configFile.skillType;
        skillCD = configFile.skillCD;
        // 核心引用
        clip = configFile.skillClip;
        _nextSkillOnFinish = configFile.nextSkillOnFinish;

        // 列表深拷贝
        jumpList = configFile.jumpList != null ? new List<Global.Jump>(configFile.jumpList) : new List<Global.Jump>();
        atkList = configFile.attackList != null ? new List<Global.Attack>(configFile.attackList) : new List<Global.Attack>();
        fxAndSoundList = configFile.fxList != null ? new List<Global.FxAndSound>(configFile.fxList) : new List<Global.FxAndSound>();

        // 自动找回模型
        if (!string.IsNullOrEmpty(configFile.previewModelName) && previewModel == null)
        {
            GameObject foundModel = GameObject.Find(configFile.previewModelName);
            if (foundModel != null)
            {
                previewModel = foundModel;
                previewAnim = previewModel.GetComponent<Animator>();
            }
        }
    }

    void SaveConfig()
    {
        if (configFile == null) return;

        configFile.skillID = skillID;
        configFile.skillName = skillName;
        configFile.skillDescription = skillDescription;
        configFile.skillType = skillTypeSelectIndex;
        configFile.skillCD = skillCD;

        configFile.jumpList = new List<Global.Jump>(jumpList);
        configFile.attackList = new List<Global.Attack>(atkList);
        configFile.fxList = new List<Global.FxAndSound>(fxAndSoundList);

        configFile.skillClip = clip;
        configFile.nextSkillOnFinish = _nextSkillOnFinish;

        if (previewModel != null) configFile.previewModelName = previewModel.name;
        else configFile.previewModelName = "";

        EditorUtility.SetDirty(configFile);
        AssetDatabase.SaveAssets();
        Debug.Log($"配置已保存: {configFile.name}");
    }
    /// <summary>
    /// 清理函数
    /// </summary>
    void CleanPreviewFx()
    {
        if (previewFxInstance != null)
        {
            DestroyImmediate(previewFxInstance);
            previewFxInstance = null;
        }
    }
    /// <summary>
    /// 从模型资源中提取所有动画片段
    /// </summary>
    void LoadClipsFromAsset()
    {
        if (animSourceModel == null) return;

        // 获取资源路径
        string assetPath = AssetDatabase.GetAssetPath(animSourceModel);
        Debug.Log(assetPath);
        // 加载该资源下的所有物体
        UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        List<AnimationClip> clipList = new List<AnimationClip>();

        foreach (var obj in objects)
        {
            // 筛选出动画片段
            if (obj is AnimationClip Aclip)
            {
                clipList.Add(Aclip);
            }
        }

        currentClips = clipList.ToArray();
    }

    void DrawFrames(AnimationClip clip)
    {
        if (clip == null) return;

        int frameCount = (int)(clip.length * clip.frameRate);
        float frameWidth = 40f;

        scrollView = EditorGUILayout.BeginScrollView(scrollView, true, true, GUILayout.Height(70));
        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < frameCount; i++)
        {
            bool selected = frameSelectIndex == i;

            // 检查当前帧是否有数据
            string markers = "";
            if (CheckKey(FindKeyIndex(i))) markers += "●\n"; // 攻击
            if (CheckFx(i)) markers += "Fx";               // 特效

            string title = $"{i}\n{markers}";

            if (GUILayout.Button(title, selected ? GUIStyles.item_selected : GUIStyles.item_normal, GUILayout.Width(frameWidth), GUILayout.Height(45)))
            {
                frameSelectIndex = i;
            }
        }

        // 更新当前帧的状态标识
        int _idx = FindKeyIndex(frameSelectIndex);
        isDrawAtk = CheckKey(_idx);
        isRewriteAtk = CheckKeyRewrite(_idx);
        keyNow = frameSelectIndex * (1.0f / clip.frameRate);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    void PlayFxAndSound(float frameIndex)
    {
        // 如果当前帧没有变化，且已经有特效在播，就不重置
        if ((int)frameIndex == lastFxFrame && previewFxInstance != null) return;

        // 先清理旧的
        CleanPreviewFx();
        lastFxFrame = (int)frameIndex;

        foreach (var t in fxAndSoundList)
        {
            if (t.keyNumber == frameIndex)
            {
                // 1. 处理特效
                if (t.particleSystem != null)
                {
                    // ★ 关键修改：实例化 Prefab 到场景中 ★
                    previewFxInstance = (GameObject)PrefabUtility.InstantiatePrefab(t.particleSystem.gameObject);

                    // 设置位置
                    if (previewModel != null)
                    {
                        previewFxInstance.transform.position = previewModel.transform.TransformPoint(t.offset);
                        if (t.followCharacter)
                            previewFxInstance.transform.parent = previewModel.transform;
                    }

                    // 重置模拟时间
                    fxDuration = 0;
                    fxLastTime = EditorApplication.timeSinceStartup;

                    // 立即模拟第一帧
                    var parts = previewFxInstance.GetComponentsInChildren<ParticleSystem>();
                    foreach (var p in parts) p.Simulate(0, true, true);
                }

                // 2. 处理音效 (不变)
                if (t.audioClip != null && as1 != null)
                {
                    as1.PlayOneShot(t.audioClip);
                }
            }
        }
    }

    int FindKeyIndex(int frame)
    {
        for (int i = 0; i < atkList.Count; i++)
        {
            if (atkList[i].keyNumber == frame) return i;
        }
        return -1;
    }

    bool CheckKey(int index) => index >= 0 && index < atkList.Count;

    bool CheckKeyRewrite(int index) => CheckKey(index) && atkList[index].isReWrite;

    bool CheckFx(int frame)
    {
        foreach (var t in fxAndSoundList) if (t.keyNumber == frame) return true;
        return false;
    }

    public static float CalcLableWidth(GUIContent label)
    {
        return GUI.skin.label.CalcSize(label).x + EditorGUI.indentLevel * GUI.skin.label.fontSize * 2;
    }

    #endregion
}

#region 辅助绘图类
// --- 辅助绘图类  ---
namespace AtkJudge
{
    public interface IJudgeArea { void SetValue(float s1, float s2, float s3, float ox, float oy, float oz); }
    public class BoxItem : IJudgeArea
    {
        public Vector3 offset, size;
        public void SetValue(float s1, float s2, float s3, float ox, float oy, float oz) { size = new Vector3(s1, s2, s3); offset = new Vector3(ox, oy, oz); }
    }
    public class SphereItem : IJudgeArea
    {
        public Vector3 offset; public float radius;
        public void SetValue(float s1, float s2, float s3, float ox, float oy, float oz) { radius = s1; offset = new Vector3(ox, oy, oz); }
    }
}
public class Judgment { public AtkJudge.IJudgeArea value; }

// --- 绘图工具类 ---
public class HandlesDrawTool : DrawTool
{
    public static HandlesDrawTool H = new HandlesDrawTool();
    public override Color color { get => Handles.color; set => Handles.color = value; }
    public override void DrawLine(Vector3 s, Vector3 e) => Handles.DrawLine(s, e);
    protected override void FillPolygon(Vector3[] v) => Handles.DrawAAConvexPolygon(v);
}
public abstract class DrawTool
{
    public static Color colorDefault = Color.white;
    public virtual Color color { get; set; }
    public bool isFill = false;
    Stack<Color> _stack = new Stack<Color>();
    public abstract void DrawLine(Vector3 s, Vector3 e);
    protected abstract void FillPolygon(Vector3[] v);
    public void PushColor(Color c) { _stack.Push(color); color = c; }
    public void PopColor() { color = _stack.Count > 0 ? _stack.Pop() : colorDefault; }

    public void DrawBox(Vector3 size, Matrix4x4 m)
    {
        Vector3[] p = MathTool.CalcBoxVertex(size, m);
        int[] idx = MathTool.GetBoxSurfaceIndex();
        for (int i = 0; i < 6; i++)
        {
            Vector3[] poly = { p[idx[i * 4]], p[idx[i * 4 + 1]], p[idx[i * 4 + 2]], p[idx[i * 4 + 3]] };
            if (isFill) FillPolygon(poly);
            for (int k = 0; k < 4; k++) DrawLine(poly[k], poly[(k + 1) % 4]);
        }
    }
    public void DrawSphere(float r, Matrix4x4 m)
    {
        DrawCircle(r, m);
        DrawCircle(r, m * Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0)));
        DrawCircle(r, m * Matrix4x4.Rotate(Quaternion.Euler(0, 90, 0)));
    }
    void DrawCircle(float r, Matrix4x4 m)
    {
        int sides = 30;
        Vector3[] v = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float rad = (i * Mathf.PI * 2) / sides;
            v[i] = m.MultiplyPoint(new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r, 0));
        }
        if (isFill) FillPolygon(v);
        for (int i = 0; i < sides; i++) DrawLine(v[i], v[(i + 1) % sides]);
    }
}
public static class MathTool
{
    /// <summary>
    /// 获取面顶点
    /// </summary>
    /// <returns></returns>
    public static int[] GetBoxSurfaceIndex() => new int[]
    {   0,1,2,3,//上
        4,5,6,7,//下
        2,6,5,3,//左
        0,4,7,1,//右
        1,7,6,2,//前
        0,3,5,4 //后
    };
    /// <summary>
    /// 计算长方体的8个顶点(相对)
    /// </summary>
    /// <param name="s"></param>
    /// <param name="m"></param>
    /// <returns></returns>
    public static Vector3[] CalcBoxVertex(Vector3 s, Matrix4x4 m)
    {
        Vector3 h = s / 2;
        Vector3[] p = { new Vector3(h.x,h.y,h.z), new Vector3(h.x,h.y,-h.z), new Vector3(-h.x,h.y,-h.z), new Vector3(-h.x,h.y,h.z),
                        new Vector3(h.x,-h.y,h.z), new Vector3(-h.x,-h.y,h.z), new Vector3(-h.x,-h.y,-h.z), new Vector3(h.x,-h.y,-h.z) };
        for (int i = 0; i < 8; i++) p[i] = m.MultiplyPoint(p[i]);
        return p;
    }
}
public static class GUIStyles { public static GUIStyle item_selected = "MeTransitionSelectHead", item_normal = "MeTransitionSelect"; }
#endregion