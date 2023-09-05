docker build --tag pedrosakuma/rinhabackend .
docker push pedrosakuma/rinhabackend
docker-compose rm -f
docker-compose down --rmi all
docker-compose up --build
