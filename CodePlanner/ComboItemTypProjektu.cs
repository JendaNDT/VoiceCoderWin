using System;

namespace CodePlanner
{
    public class ComboItemTypProjektu
    {
        public string Klic { get; set; } = "";
        public string Nazev { get; set; } = "";
        public override string ToString() => Nazev;
    }
}
