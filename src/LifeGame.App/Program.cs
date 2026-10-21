// 引数で盤面のサイズを変えられる(例: LifeGame.App 4096 4096)。幅は64の倍数
int width = args.Length >= 1 && int.TryParse(args[0], out int w) ? w : 2048;
int height = args.Length >= 2 && int.TryParse(args[1], out int h) ? h : width;

using var game = new LifeGame.App.LifeGameApp(width, height);
game.Run();
