using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// для активации затемнения, нужно будет просто включить объект
public class Screen_fader : MonoBehaviour
{
    public float fade_speed = 1f;

    IEnumerator Start()
    {
        Image fade_image = GetComponent<Image>();
        Color color = fade_image.color;

        while(color.a < 1f)
        {
            color.a += fade_speed * Time.deltaTime;
            fade_image.color = color;
            yield return null;
        }

        print("Затемнение завершено !");
    }
}