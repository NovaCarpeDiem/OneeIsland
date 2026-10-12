Add-Type -AssemblyName System.Drawing, System.Windows.Forms
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$w = 750
$h = 240
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$srcX = [int](($screen.Width - $w) / 2)
$g.CopyFromScreen($srcX, 0, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$target = "C:\Users\ali\.gemini\antigravity\brain\171063e4-a201-452f-807c-7c4ac9e8169e\live_notch_redesign.png"
$bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
Write-Output "Saved to $target"
