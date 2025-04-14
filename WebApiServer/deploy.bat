if exist "publish" (
	ssh ubuntu@vps-c0b86ff8.vps.ovh.us "sudo systemctl stop PluginLoader.service"
	timeout 1
	ssh ubuntu@vps-c0b86ff8.vps.ovh.us "systemctl status PluginLoader.service"
	scp -r publish/* ubuntu@vps-c0b86ff8.vps.ovh.us:/home/ubuntu/server/
	ssh ubuntu@vps-c0b86ff8.vps.ovh.us "sudo systemctl start PluginLoader.service"
	timeout 5
	ssh ubuntu@vps-c0b86ff8.vps.ovh.us "systemctl status PluginLoader.service"
)

pause