using LaboratoireProgrammation.Project.ViewModels.Menu;

namespace LaboratoireProgrammation.Project.Models.Menu;

public class RunSubprograms {
    public enum Subprograms {
        Exercice1,
        Exercice2,
        Exercice3,
        Exercice4,
        Exercice5,
        Exercice6,
        Exercice7,
        Exercice8,
        Exercice9,
        Exercice10,
        Labo1,
        Memfy,
        OverseerWars,
        ESP8266,
        LabHospital,
        FlashMaster,
        Uplink,
        Exercice1B3,
        Exercice2B3
    }

    public static void RunSub(Subprograms sub, MainWindow win) {
        win.OnExerciseOpened();
        switch (sub) {
            case Subprograms.Exercice1:
                _ = MenuLoaders.Run_Exo1(win);
                break;

            case Subprograms.Exercice1B3:
                _ = MenuLoaders.Run_Exo1B3(win);
                break;
            
            case Subprograms.Exercice2B3:
                _ = MenuLoaders.Run_Exo2B3(win);
                break;

            case Subprograms.Exercice2:
                _ = MenuLoaders.Run_Exo2(win);
                break;

            case Subprograms.Exercice3:
                _ = MenuLoaders.Run_Exo3(win);
                break;

            case Subprograms.Exercice4:
                // _ = MenuLoaders.Run_Exo4(win);
                break;

            case Subprograms.Exercice5:
                _ = MenuLoaders.Run_Exo5(win);
                break;

            case Subprograms.Exercice6:
                _ = MenuLoaders.Run_Exo6(win);
                break;

            case Subprograms.Exercice7:
                _ = MenuLoaders.Run_Exo7(win);
                break;

            case Subprograms.Exercice8:
                _ = MenuLoaders.Run_Exo8(win);
                break;

            case Subprograms.Exercice9:
                _ = MenuLoaders.Run_Exo9(win);
                break;

            case Subprograms.Exercice10:
                _ = MenuLoaders.Run_Exo10(win);
                break;

            case Subprograms.Labo1:
                RunLab1.Run(win);
                break;

            case Subprograms.OverseerWars:
                _ = MenuLoaders.RunOverseerWars(win);
                break;

            case Subprograms.LabHospital:
                _ = MenuLoaders.RunHospital(win);
                break;

            case Subprograms.FlashMaster:
                _ = MenuLoaders.RunFlashMaster(win);
                break;

            case Subprograms.ESP8266:
                _ = MenuLoaders.Run_ESP8266(win);
                break;
            
            case Subprograms.Uplink:
                _ = MenuLoaders.RunUplink(win);
                break;
        }
    }
}