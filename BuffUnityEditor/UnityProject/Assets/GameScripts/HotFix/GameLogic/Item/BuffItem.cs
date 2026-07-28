using UnityEngine;

namespace GameLogic.Item
{
    /// <summary>
    /// Buff道具组件，挂载在道具预制体上，记录对应的Buff Key和拾取配置。
    /// </summary>
    public class BuffItem : MonoBehaviour
    {
        [Tooltip("对应的Buff Key或Id")]
        [SerializeField] private string buffKey;

        [Tooltip("拾取后是否销毁道具")]
        [SerializeField] private bool destroyOnPickup = true;

        [Tooltip("勾选后拾取仅SetActive(false)，用于对象池复用")]
        [SerializeField] private bool disableInsteadOfDestroy;

        public string BuffKey => buffKey;

        /// <summary>
        /// 拾取处理，返回true表示已处理（销毁或禁用）。
        /// </summary>
        public bool Pickup()
        {
            if (disableInsteadOfDestroy)
            {
                gameObject.SetActive(false);
                return true;
            }

            if (destroyOnPickup)
            {
                Destroy(gameObject);
                return true;
            }

            return false;
        }
    }
}
