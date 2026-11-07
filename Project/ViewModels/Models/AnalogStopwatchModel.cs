namespace LaboratoireProgrammation.Project.ModernOverseerWarsMP.Models {
    public class AnalogStopwatchModel {
        public int ElapsedSeconds { get; set; } = 0;
        public bool IsRunning { get; set; } = false;
        public void Increment() { ElapsedSeconds++; }

        public void Reset() {
            ElapsedSeconds = 0;
            IsRunning = false;
        }
    }
}