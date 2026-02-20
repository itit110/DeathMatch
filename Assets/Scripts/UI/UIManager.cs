using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UIİ’è")]
    [SerializeField] public TMP_Text ammoText; //@’e–ò‚Ì•\¦UI

    public void SettingBulletText(int ammoClip, int reserveAmmo)
    {
        ammoText.text = ammoClip + "/" + reserveAmmo; //@ƒ}ƒKƒWƒ““à‚Ì’e” / Š’e”
    }

}
