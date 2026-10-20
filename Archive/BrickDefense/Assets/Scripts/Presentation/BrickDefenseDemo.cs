using BrickDefense.Presentation;
using BrickDefense.Workflow;
using BrickDefense.Core;
using UnityEngine;

namespace BrickDefense.Demo
{
    // Scene composition only. The existing script GUID remains attached to the scene.
    public sealed class BrickDefenseDemo : MonoBehaviour
    {
        private LevelView view;
        private CombatView combat;
        [SerializeField] private int initialCoreHealth=100,towerDamage=12;
        [SerializeField] private float towerRange=8,towerAttackInterval=0.45f;
        public LevelSession Session { get; private set; }
        public BattleSession Battle { get; private set; }

        // 场景入口只负责装配模块并订阅状态变化，关卡数据由 Session 持有。
        private void Start()
        {
            Application.targetFrameRate=60;
            Session=new LevelSession();
            view=gameObject.AddComponent<LevelView>();
            Session.Changed+=Display;
            var photoPanel=gameObject.AddComponent<PhotoScanPanel>();
            photoPanel.Initialize(Session);
            Battle=new BattleSession(Session);
            combat=gameObject.AddComponent<CombatView>(); combat.Initialize(Battle);
            var battlePanel=gameObject.AddComponent<BattlePanel>();
            battlePanel.Initialize(Battle,Session,combat,photoPanel,
                new CombatConfig(initialCoreHealth,towerDamage,towerRange,towerAttackInterval));
            gameObject.AddComponent<DemoUI>().Initialize(Session,view,photoPanel,battlePanel);
            Session.LoadExample(false);
        }

        private void Display(LevelContext context) { view.Display(context.Result); combat?.ClearSelection(); }
        private void OnDestroy() { Battle?.Dispose(); if (Session != null) Session.Changed-=Display; }
    }
}
