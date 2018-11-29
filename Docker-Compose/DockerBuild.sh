cp -r ../ServerServerProxy .
docker build . -t serverserver:latest -f Dockerfile.serverserver 
#docker build . -t nginx-vnc-cloud:latest -f Dockerfile.nginx-vnc-cloud
docker build . -t novnc:latest -f Dockerfile.novnc