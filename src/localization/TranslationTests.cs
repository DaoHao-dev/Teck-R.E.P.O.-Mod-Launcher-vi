using System;
using Teck.Vietnamese;
class TranslationTests {
    static void Expect(string original,string expected){string actual=TextMap.Translate(original);if(actual!=expected)throw new Exception(original+" -> "+actual+"; expected "+expected);}
    static int Main(){
        Expect("切换中文","VI / EN");Expect("切换英文","VI / EN");
        Expect("Switch to Chinese","VI / EN");Expect("切换语言","Ngôn ngữ");
        Expect("<b>切换中文</b>","<b>VI / EN</b>");
        Expect("Item Control","Vật phẩm");Expect("Game Visuals","Hiển thị");
        TextMap.Vietnamese=false;
        Expect("Item Control","Item Control");Expect("Game Visuals","Game Visuals");
        Expect("Clear All Tokens","Clear All Tokens");Expect("切换中文","VI / EN");
        TextMap.Vietnamese=true;
        Expect("Clear All Tokens","Xóa toàn bộ xu");Expect("Tokens (Total: 100)","Xu trang trí (Tổng: 100)");
        Expect("<color=#FFCC00>Unlock All</color>","<color=#FFCC00>Mở khóa tất cả</color>");
        Expect("All unlocked, 123 new.","Đã mở khóa tất cả, thêm 123 món.");
        Expect("Showing 42 cosmetics, 3 sections","Đang hiện 42 món trang trí, 3 mục");
        Expect("Enable Shared Upgrades","Bật chia sẻ nâng cấp");Expect("Alive Enemies (12)","Quái còn sống (12)");
        Expect("SharedUpgradesPlus.owner","SharedUpgradesPlus.owner");Expect("GT_EyeBigPupil","GT_EyeBigPupil");
        Expect("Max Players","Số người tối đa");Expect("Max Players = 10","Số người tối đa = 10");
        Expect("Mở khóa tất cả","Mở khóa tất cả");Expect("<sprite name=\"Health\">Health","<sprite name=\"Health\">Máu");
        Console.WriteLine("Translation tests passed; "+TextMap.Count+" dictionary entries.");return 0;
    }
}
