using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;


public static class EX
{
    //swiftのthenや、kotlinのwithっぽい仕組み。
    public static T Then<T>(this T self, Action<T> func)
    {
        func.Invoke(self);
        return self;
    }
}


public class rootScript : MonoBehaviour
{
    //宝石のID
    private static readonly int[] GEMIDS = new int[] { 107, 108, 109, 110, 111, 112, 113, };

    //魚が泳ぐ速さ
    private static readonly float FISH_SPEED = 0.9f;

    //泳ぐ魚が左右を切り返す座標
    private static readonly float FISH_LEFT_LIMIT = -40;
    private static readonly float FISH_RIGHT_LIMIT = 5;



    //釣れる魚の画像リスト
    public List<Sprite> _catchFishList;

    //数字の画像リスト
    public List<Sprite> _number;

    //宝石の画像リスト(右上に表示されるやつ)
    public List<Sprite> _gem;

    //主人公の成長の画像リスト
    public List<Sprite> _hero;

    //主人公のレベルが上った時に表示されるキラキラの画像
    public Sprite _kirakira;

    //キラキラを複数表示するためのゲームオブジェクトプール
    private List<GameObject> _kirakira_pool = new List<GameObject>();

    //主人公のレベル＝どの画像を使用するのかを示すIndex
    private int _level = 0;

    //持っている宝石のID
    private Dictionary<int, GameObject> _gemHave = new Dictionary<int, GameObject>();

    //泳いでる魚の情報
    public Sprite _fishL; //左向きの魚画像
    public Sprite _fishR; //右向きの魚画像
    public GameObject _swimFish; //魚のゲームオブジェクト
    private float _dirc = 1; //向き

    //釣り上げ操作用
    public GameObject _rope; //釣り糸のゲームオブジェクト
    public GameObject _uki; //浮きのゲームオブジェクト
    public GameObject _caughtFish; //釣り上げた魚のゲームオブジェクト
    private float _power = 0; //引っ張る力(1==上端、0==下端)

    //釣り上げた魚の数
    private int _count = 0;


    // Use this for initialization
    void Start()
    {
        Application.targetFrameRate = 60;

        //右上の宝石を用意。
        PrepareSmallGem();

        //キラキラを用意
        PrepareKirakira();

        //状態の読み込み
        Load();

        //釣り上げ数のカウント
        UpdateCount();

        //見た目を更新
        updateHero();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateFishSwim();
        UpdateKirakira();
        CatchCheck();
        Controll();
    }


    /////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////

    //保存
    private void Save()
    {
        _gemHave.ToList().ForEach(A => { PlayerPrefs.SetInt("GEM" + A.Key, A.Value.activeSelf ? 1 : 0); });
        PlayerPrefs.SetInt("LEVEL", _level);
        PlayerPrefs.SetInt("COUNT", _count);
        PlayerPrefs.Save();
    }

    //読み込み
    private void Load()
    {
        _count = PlayerPrefs.GetInt("COUNT", 0);
        _level = PlayerPrefs.GetInt("LEVEL", 0);
        _gemHave.ToList().ForEach(A => { A.Value.SetActive(PlayerPrefs.GetInt("GEM" + A.Key, 0) == 1); });
    }

    /////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////

    //SpriteRenderer付きゲームオブジェクト生成
    private GameObject FindOrCreateGameObjectWithSpriteRenderer(string gameobjectName, Vector3 pos, Sprite sprite, int sortOrder)
    {
        return GameObject.Find(gameobjectName) ?? new GameObject(gameobjectName).Then(g =>
        {
            g.transform.position = pos;
            g.SetActive(false);
            g.AddComponent<SpriteRenderer>().Then(x =>
            {
                x.sprite = sprite;
                x.sortingOrder = sortOrder;
            });
        });
    }

    //画面右上に表示する小さいgemの準備
    private void PrepareSmallGem()
    {
        for (int I = 0; I < GEMIDS.Length; ++I)
        {
            int id = GEMIDS[I];
            _gemHave.Add(id, FindOrCreateGameObjectWithSpriteRenderer("gem" + id, new Vector3(39 - I * 2, 125, 0), _gem[I], 2000));
        };
    }

    //キラキラを用意
    private void PrepareKirakira()
    {
        _kirakira_pool.Add(FindOrCreateGameObjectWithSpriteRenderer("kirakira1", new Vector3(29, 33, 0), _kirakira, 1500));
        _kirakira_pool.Add(FindOrCreateGameObjectWithSpriteRenderer("kirakira2", new Vector3(19, 37, 0), _kirakira, 1500));
    }

    /////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////

    //泳ぐ魚の動作
    private void UpdateFishSwim()
    {
        var p = _swimFish.transform.position;

        //左端に到達したので右向きに。
        if (p.x < FISH_LEFT_LIMIT)
        {
            _swimFish.GetComponent<SpriteRenderer>().sprite = _fishR;
            _dirc = FISH_SPEED;
            p.x = FISH_LEFT_LIMIT;
        }
        else
        //右端に到達したので左向きに。
        if (FISH_RIGHT_LIMIT < p.x)
        {
            _swimFish.GetComponent<SpriteRenderer>().sprite = _fishL;
            _dirc = -FISH_SPEED;
            p.x = FISH_RIGHT_LIMIT;
        }

        //移動
        p.x += _dirc;
        _swimFish.transform.position = p;
    }


    //キラキラを適当に点滅させる。
    private void UpdateKirakira()
    {
        //レベル１以下ならキラキラ無し。
        if (_level <= 1)
        {
            return;
        }

        //適当に。
        var t = Time.time % 1.0;
        var s = _kirakira_pool.Count;
        var r0 = 1.0 / (s + 1);
        for (int I = 0; I < s; ++I)
        {
            _kirakira_pool[I].SetActive((I * r0) < t && t < ((I + 1) * r0));
        }
    }


    //釣り上げ操作
    public void Controll()
    {
        //釣り糸を引き上げる力[_power]を計算
        var input = Input.GetMouseButton(0);
        var to = (input) ? 0.0f : 1.0f;
        _power += (to - _power) * 0.6f;

        //ロープの長さを伸縮
        var s = _rope.transform.localScale;
        s.y = 0.3f + (_power * 1.3f);
        _rope.transform.localScale = s;

        //浮きの位置を調整
        var p = _uki.transform.position;
        p.y = 48 - 38 * _power;  //←目分量
        _uki.transform.position = p;

        //ロープの伸縮に合わせて「釣れた魚」の表示位置も更新。
        if (_caughtFish.activeSelf)
        {
            p.x += 2;
            p.x -= _caughtFish.GetComponent<SpriteRenderer>().sprite.rect.width / 2;
            p.y -= 7;
            _caughtFish.transform.position = p;
        }

        // 入力がなくなったら「釣れた魚」を非表示、「泳いでる魚」を表示する。
        if (!input)
        {
            _caughtFish.SetActive(false);
            _swimFish.SetActive(true);
        }
    }

    //釣り上げ判定
    public void CatchCheck()
    {
        //クリックした瞬間だけ判定。
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        //釣り針が海に入ってるのが条件
        if (_power < .9f)
        {
            return;
        }

        //魚が釣れる範囲
        var p = _swimFish.transform.position;
        if (p.x < -23)
        {
            return;
        }

        if (-10 < p.x)
        {
            return;
        }

        //「釣れた魚」を表示、「泳いでる魚」を非表示にする。
        _caughtFish.SetActive((true));
        _swimFish.SetActive(false);

        //釣り上げ回数によって釣れる魚がどんどん変わる仕組み
        int limit = Math.Min(_count / 7 + 8, _catchFishList.Count);
        int idx = (int)(Math.Abs(UnityEngine.Random.value * Int16.MaxValue)) % limit;

        //釣り上がる魚の見た目を変更。
        _caughtFish.GetComponent<SpriteRenderer>().sprite = _catchFishList[idx];

        //釣り上げ回数を＋１
        ++_count;

        //釣り上げ回数の表示を更新
        UpdateCount();

        //宝石の取得チェック
        AddGemAndCompProcess(idx);

        //操作ごとにゲームを保存
        Save();
    }


    //釣り上げ回数のカウントと数値を表示
    public void UpdateCount()
    {
        int v = _count;
        int digit = 0;
        while (0 < v)
        {
            digit++;

            //現在の桁の値を取得
            int digitVal = (v % 10);
            v = v / 10;

            //[digit]桁目のゲームオブジェクトの名前
            var digitName = "digit_" + digit;

            //数字を表示
            FindOrCreateGameObjectWithSpriteRenderer(digitName, new Vector3(42 - digit * 10, -20, 0), null, 2000).Then(x =>
            {
                x.GetComponent<SpriteRenderer>().sprite = _number[digitVal];
                x.SetActive(true);
            });
        }
    }

    //宝石の取得とコンプリート処理
    private void AddGemAndCompProcess(int idx)
    {
        //釣れた宝石を右上に表示
        GEMIDS.Where(a => a == idx).ToList().ForEach(a => { _gemHave[a].SetActive(true); });

        //コンプしたらレベルアップ
        if (_gemHave.All(a => a.Value.activeSelf))
        {
            //宝石の所持状態をリセット
            _gemHave.ToList().ForEach(a => { a.Value.SetActive(false); });

            //猫をレベルアップ
            _level = _level + 1;

            //見た目を更新
            updateHero();
        }

    }

    //レベルに合わせた見た目に変更
    private void updateHero()
    {
        GameObject.Find("hero").GetComponent<SpriteRenderer>().sprite = _hero[Math.Min(_hero.Count - 1, _level)];
    }

}
