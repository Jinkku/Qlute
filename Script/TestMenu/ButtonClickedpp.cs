using Godot;
using System;
using System.Linq;
public partial class ButtonClickedpp : Button
{
    private void _on_pressed(){
        using var file = FileAccess.Open((string)SettingsOperator.Beatmaps[(int)GetMeta("SongID")]["rawurl"], FileAccess.ModeFlags.Read);
        var timetotal = (int)SettingsOperator.Beatmaps[(int)GetMeta("SongID")]["timetotal"];
        var text = file.GetAsText();
        var lines = text.Split("\n");
        Notify.Post("Clicked "+ GetMeta("SongID"));
        var isHitObjectSection = false;
        var ppvalue = 0f;
        int hitob = 0;
        var skip = timetotal / 16;
        var skipid = 0;
        foreach (Label ix in Main.PostMarks){
            ix.Text = "0pp";
        }
        foreach (var line in lines)
        {            if (line.Trim() == "[HitObjects]")
            {
                isHitObjectSection = true;
                continue;
            }

            if (isHitObjectSection)
            {
                // Break if we reach an empty line or another section
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("["))
                {
                    isHitObjectSection = !isHitObjectSection;
                    break;
                }
                var notecfg = line.Split(new[] { ',', ':' });
                hitob++;
                if (Int32.Parse(notecfg[2])/skip > skipid){
                    GD.Print(skipid);
                    skipid++;
                }
                ppvalue = SettingsOperator.Get_ppvalue(hitob,0,0,0,combo: hitob);
                ((Label)Main.PostMarks[skipid]).Text = ppvalue.ToString("N0") + "pp";
                ((ColorRect)Main.GraphBars[skipid]).Size = new Vector2(10,ppvalue/10);
            }}

    }
}