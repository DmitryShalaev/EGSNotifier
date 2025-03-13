namespace Core.Jobs {

    /// <summary>
    /// Статический класс <c>Job</c> используется для инициализации и запуска запланированных задач.
    /// </summary>
    public static class Job {

        /// <summary>
        /// Асинхронный метод для инициализации и запуска запланированных заданий.
        /// </summary>
        /// <returns>Задача, представляющая асинхронную операцию.</returns>
        public static async Task InitAsync() => await UpdateEGSJob.StartAsync();
    }
}
