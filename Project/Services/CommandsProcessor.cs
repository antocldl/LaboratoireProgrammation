using System.Windows.Media;
using LaboratoireProgrammation.Project.Models.Menu;
using LaboratoireProgrammation.Project.Models.Miscellaneous;
using LaboratoireProgrammation.Project.ViewModels.Laboratoires.OverseerWars;
using LaboratoireProgrammation.Project.ViewModels.Menu;

namespace LaboratoireProgrammation.Project.Services;

public class CommandsProcessor {
    public static async void ProcessCommand(string cmd, MainWindow window) {
        switch (cmd) {
            case "help":
                TerminalDisplay.AppendOutput("COMMAND LIST:");
                TerminalDisplay.BlankSpace();
                TerminalDisplay.AppendOutput("  help    - Display this database");
                TerminalDisplay.AppendOutput("  clear   - Purge screen buffer");
                TerminalDisplay.AppendOutput("  font    - Adjust terminal readability");
                TerminalDisplay.AppendOutput("  exit    - Terminate session");
                TerminalDisplay.AppendOutput("  about   - System info");
                TerminalDisplay.AppendOutput("  b2      - Open Menu Bloc 2");
                TerminalDisplay.AppendOutput("  b3      - Open Menu Bloc 3");
                TerminalDisplay.AppendOutput("  menu    - Return to main terminal");
                TerminalDisplay.BlankSpace();
                TerminalDisplay.AppendOutput("  exo1    - Launch Exo (1+B)");
                TerminalDisplay.AppendOutput("  exo1b3  - Launch Exo (1-B3 Mail Dispatch)");
                TerminalDisplay.AppendOutput("  exo2    - Launch Exo (2)");
                TerminalDisplay.AppendOutput("  exo3    - Launch Exo (3)");
                TerminalDisplay.AppendOutput("  exo4    - Launch Exo (4)");
                TerminalDisplay.AppendOutput("  exo5    - Launch Exo (5)");
                TerminalDisplay.AppendOutput("  exo6    - Launch Exo (6)");
                TerminalDisplay.AppendOutput("  exo7    - Launch Exo (7)");
                TerminalDisplay.AppendOutput("  exo8    - Launch Exo (8)");
                TerminalDisplay.AppendOutput("  exo9    - Launch Exo (9)");
                TerminalDisplay.AppendOutput("  exo10   - Launch Exo (10)");
                TerminalDisplay.AppendOutput("  lab1    - Launch Lab (1)");
                TerminalDisplay.AppendOutput("  ow      - Launch Overseer Wars");
                TerminalDisplay.AppendOutput("  owmp    - Launch Overseer Wars Multiplayer");
                TerminalDisplay.AppendOutput("  flash   - Launch FlashMaster");
                TerminalDisplay.AppendOutput("  uplink  - Launch Uplink");
                TerminalDisplay.AppendOutput("  hospi   - Launch Hospital");
                break;

            case "info":
                TerminalDisplay.AppendOutput("ROBCO UNIFIED OPERATING SYSTEM v1.0");
                TerminalDisplay.AppendOutput("Running on WPF .NET Core.");
                TerminalDisplay.BlankSpace();
                TerminalDisplay.AppendOutput(
                    "• [exo.1+b] : UI Interactive : Contrôle de boutons et permutation d'images", Colors.Coral);
                TerminalDisplay.AppendOutput(
                    "• [exo.2]   : Simulation de transfert de fichiers (Barres de progression)", Colors.Coral);
                TerminalDisplay.AppendOutput(
                    "• [lab.1]   : Système de gestion de BDD (CRUD Complet) [EN DÉVELOPPEMENT]", Colors.Gold);
                break;

            case "clear":
                window.OutputBox.Document.Blocks.Clear();
                break;

            case "font":
                ConsoleBehavior.IsAdjustingSize = true;
                TerminalDisplay.AppendOutput("[SYSTEM] FONT ADJUSTMENT MODE ENGAGED.", Colors.Yellow);
                TerminalDisplay.AppendOutput("Use [UP/DOWN] to scale text. Press [ENTER] to confirm.", Colors.Yellow);
                break;

            case "about":
                TerminalDisplay.AppendOutput("ROBCO UNIFIED OPERATING SYSTEM v1.0");
                TerminalDisplay.AppendOutput("Running on WPF .NET Core.");
                break;

            case "exit":
                window.Close();
                break;

            case "exo1":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [1+B]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice1, window);
                break;

            case "exo2":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [2]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice2, window);
                break;

            case "exo3":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [3]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice3, window);
                break;

            case "exo4":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [4]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice4, window);
                break;

            case "exo5":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [5]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice5, window);
                break;

            case "exo6":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [6]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice6, window);
                break;

            case "lab1":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> LABORATOIRE [1]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunLab1.Run(window);
                break;

            case "memfy":
            case "memfyai":
            case "memfy ai":
                RunMemfy.Run(window);
                break;

            case "b2":
            case "bloc2":
            case "menu2":
                window.SetVisualMenu(VisualMenuState.Bloc2);
                break;

            case "b3":
            case "bloc3":
            case "menu3":
                window.SetVisualMenu(VisualMenuState.Bloc3);
                break;

            case "menu":
            case "home":
                window.MainInit();
                break;

            case "exo1b3":
            case "exo1-b3":
            case "exo1_b3":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [1-B3] (MAIL DISPATCH)", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice1B3, window);
                break;

            case "exo7":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [7]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice7, window);
                break;

            case "exo8":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [8]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice8, window);
                break;

            case "exo9":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [9]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice9, window);
                break;

            case "exo10":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> EXO [10]", Colors.Yellow);
                TerminalDisplay.AppendOutput("...", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Exercice10, window);
                break;

            case "flash":
            case "flashmaster":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> FLASHMASTER", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.FlashMaster, window);
                break;

            case "uplink":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> UPLINK", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.Uplink, window);
                break;

            case "hospi":
            case "hospital":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> HOPITAL", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.LabHospital, window);
                break;

            case "esp":
            case "esp8266":
                TerminalDisplay.AppendOutput("[SYSTEM] DÉMARRAGE >>> ESP8266", Colors.Yellow);
                RunSubprograms.RunSub(RunSubprograms.Subprograms.ESP8266, window);
                break;

            case "sep":
                TerminalDisplay.SeparationLine();
                break;

            case "ow":
                OverseerWarInit.Introduce(window);
                break;

            case "owmp":
            case "ow-mp":
            case "overseerwarsmp":
                var mpMenu = new LaboratoireProgrammation.Project.ModernOverseerWarsMP.Views.OverseerWarsMpMenuWindow();
                mpMenu.Show();
                break;

            case "te":
                break;

            default:
                TerminalDisplay.AppendOutput($"[ERROR] COMMAND '{cmd}' UNRECOGNIZED.", Colors.Red);
                break;
        }

        TerminalDisplay.BlankSpace();
    }
}